# Writing tests with TUnit

The `E2E.TUnit` package holds `E2E.TUnit.E2ETest`, a TUnit base class. Topic
`writing-tests` covers what every framework shares: locators, reads,
expectations, agent steps, and the `Browser` member.

## Contents

- Project
- The base class
- Soft checks and polls
- Saved sessions
- The run fixture
- A complete example
- Run
- Mobile and Windows

## Project

```bash
dotnet new console -n MyApp.E2E
dotnet add MyApp.E2E package TUnit
dotnet add MyApp.E2E package E2E
dotnet add MyApp.E2E package E2E.TUnit
```

TUnit runs on Microsoft.Testing.Platform, so the project is an executable
(`<OutputType>Exe</OutputType>`), and `dotnet run` or `dotnet test` starts it.
Put `e2e.config.json` in the project folder. Topic `setup` covers the config
and the browser install.

The session stops on the test's cancellation token, so calls on `App`,
`Screen`, `Agent`, `Browser`, and `Expect.That` do not need a token.
`Expect.Poll` matchers do not see the session: pass the test's token to
`ToSatisfyAsync` or `ToBeAsync` so a cancelled run stops them. Write
`TUnit.Core.TestContext` in full, because `using E2E;` brings in
`E2E.TestContext` too.

## The base class

Derive each test class from `E2ETest`:

```csharp
using E2E;
using E2E.TUnit;

public sealed class BillingTests : E2ETest
{
}
```

- TUnit builds a new instance of the class for each test. A before-test hook
  of `E2ETest` starts one engine session, so every test gets a fresh browser
  context. An after-test hook commits the replay cache from the TUnit result:
  a pass or a skip records verified acts, a failure evicts the unverified
  ones, and a cancelled test leaves the cache alone.
- Your own `[Before(Test)]` hook runs after the session starts, and your own
  `[After(Test)]` hook runs before the session is committed. A check in an
  `[After(Test)]` hook after a failure verifies nothing.
- The first attempt can replay. The attempts of `[Retry]` run live and still
  record.
- The default cache title is the class and method name with every argument
  (`[Arguments]`, a data source) in full, so each row keeps its own replay.
  Override `CacheTitle(TestContext)` to change it.
- `[Category("RealModel")]` marks a test that needs a real model.
- The fixture reads the same settings as the other frameworks: override a
  property such as `Config`, `BaseUrl`, or `CacheMode` on the class.
- When a test body fails, the app is looked at the moment it fails, before
  your `[After(Test)]` hook can navigate away, and the evidence goes in the
  failure.

## Soft checks and polls

- `Expect.Soft` failures do not stop the body. The test fails when its
  after-test hook runs, in one `ASSERTION_FAILED` that lists every soft
  failure. A soft failure before `Skip.Test(...)` fails the test, and the skip
  reason stays in the message.
- `Expect.Poll` takes a predicate through `ToSatisfyAsync`. There is no
  `ToMatchAsync`.

## Saved sessions

Sign in once, then start other tests signed in (upstream's `test.setup` and
`session`). Mark the sign-in test `[Test, E2ESetup("title", Saves = ["admin"])]`:
TUnit does not let a library define a test attribute, so `[E2ESetup]` goes
beside `[Test]`, and its title becomes the display name. The body signs in,
checks that it worked, and calls `await SaveSessionAsync("admin")` once for each
name in `Saves`. Mark a test or a class `[E2ESession("admin")]` to restore it
before the body runs.

```csharp
public sealed class Dashboard : E2ETest
{
    [Test, E2ESetup("authenticate as admin", Saves = ["admin"])]
    public async Task Authenticate()
    {
        var admin = Credentials.User("admin");
        await App.OpenAsync("/login");
        await Screen.GetByLabel("Username").FillAsync(admin.Username);
        await Screen.GetByLabel("Password").FillAsync(admin.Password);
        await Screen.GetByRole("button", "Sign in").TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/dashboard");
        await SaveSessionAsync("admin");
    }

    [Test]
    [E2ESession("admin")]
    public async Task Opens_the_dashboard_directly()
    {
        await App.OpenAsync("/dashboard");
    }
}
```

- A name saved twice, a name not in `Saves`, or a name in `Saves` the body
  never saved fails the setup test with `SESSION_CONTRACT`. `SaveSessionAsync`
  outside a setup test fails the same way. Nothing is saved when the setup
  test fails.
- A setup test gets the highest execution priority (`Priority.Critical`), so
  TUnit schedules it before the other tests. A test that restores a session
  nobody saved yet runs the setup test that declares it first, once per run, so
  a dependent can run alone (upstream's runner schedules the setup before its
  dependents too). The test that needs it runs the setup itself: it builds the
  setup's class with its parameterless constructor and runs the method in a
  session of its own, without the class's own `[Before(Test)]` and
  `[After(Test)]` hooks, and TUnit then skips that setup test when its turn
  comes. Exactly one setup test of the test assembly must declare the name:
  with none, or with two, the restore fails with `SESSION_UNAVAILABLE`. A setup
  test that fails fails each of its dependents with `SESSION_UNAVAILABLE`
  (upstream skips them as `setup-failed`). A class-level `[E2ESession]` does not
  apply to the setup tests of that class.
- TUnit runs tests in parallel, so the setup test and a test that restores its
  session can start at the same moment, and the setup then runs twice. Put
  `[NotInParallel]` on the class when it must not.
- A restore leaves no page open: call `App.OpenAsync` first. A session holds
  what the browser keeps (cookies, local storage, IndexedDB), not server state.
- An engine without a state capability (`stateCapability` false in
  `report.json`) fails `SaveSessionAsync` and a restore with
  `UNSUPPORTED_CAPABILITY`. `WebEngine` has it.
- A session lasts one run. It is encrypted with a key held in memory and kept
  under `.e2e/sessions/<run id>`. Derive an `E2ERunFixture` to delete it when
  the run ends.

## The run fixture

A run-level hook calls the `reporters` of `e2e.config.json` when the run ends,
stops the app commands that the targets declare when the last test is done (not
when the process exits), and carries the `Filters`, `ChangedSince`, and
`LastFailed` selection (topic `running`). TUnit has no assembly fixture to
register, and a hook that a library declares runs in every project that
references it, so the hook is opt-in by a class: derive a public, non-abstract
class with a public parameterless constructor from `E2ERunFixture` anywhere in
the test project.

```csharp
using E2E.TUnit;

public sealed class AppCommands : E2ERunFixture;
```

- A `[Before(TestSession)]` hook creates every such class of the test assembly
  and starts it before the first test; an `[After(TestSession)]` hook finishes
  it after the last. A project with no such class runs none of it.
- To give the run test filters, override `Filters` (and `ChangedSince`,
  `LastFailed`, `Config`) in the derived class, as in the other frameworks.
- The fixture finds the tests by reflection: every public method of a concrete
  `E2ETest` with a `[Test]`, not `[Explicit]` (a parameterized method is one
  test). `Tags` are the `[Category]` values of the method, its class, its
  enclosing classes, and the assembly. A `[Skip("reason")]` is an outright skip;
  a derived skip attribute decides when the test runs. `--treenode-filter` does
  not narrow what the fixture judges.
- A test the filters left out ends with `Skip.Test(reason)` before its session
  starts, as does a setup test that already ran for a dependent.

## A complete example

```csharp
using E2E;
using E2E.TUnit;

public sealed class DaritenTests : E2ETest
{
    [Test]
    public async Task The_dashboard_links_to_the_transaction_register()
    {
        await App.OpenAsync("/");
        await Expect.That(Screen.GetByRole("heading", "Dashboard")).ToBeVisibleAsync();

        await Screen.GetByRole("link", "Transactions").ClickAsync();
        await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
    }

    [Test]
    [Category("RealModel")]
    public async Task An_agent_filters_the_register_by_category()
    {
        await App.OpenAsync("/transactions");
        await Agent.ActAsync("filter the register to show only the Groceries category");
        await Agent.AssertAsync("every transaction listed is in the Groceries category");
    }
}
```

`samples/E2E.TUnit.Sample` in the e2e-dotnet repo holds a longer version.

## Run

```bash
dotnet test
dotnet test --treenode-filter "/*/*/*/*[Category!=RealModel]"   # skip the real-model tests
dotnet test --treenode-filter "/*/*/*/*[Category=RealModel]"    # only the real-model tests
```

On .NET SDK 10 and later, `dotnet test` refuses a Microsoft.Testing.Platform
project unless a `global.json` next to the solution opts in:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

Without it, the error reads "Testing with VSTest target is no longer
supported". `dotnet run --project MyApp.E2E -- --treenode-filter "..."` needs
no `global.json`.

## Mobile and Windows

`E2E.Mobile.TUnit` holds `MobileAppTest`, and `E2E.WinApp.TUnit` holds
`WinAppTest`. Both derive from `E2E.TUnit.E2ETest`, and topics `mobile` and
`windows` cover their members. TUnit runs base-class hooks first, so each one
installs or opens the app in its own `[Before(Test)]` hook after the session
starts, and `MobileAppTest` leases a hosted device when the engine is created.
