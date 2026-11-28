# Writing tests with xUnit v3

The `E2E.XUnit.V3` package holds `E2E.XUnit.V3.E2ETest`, an xUnit v3 base
class. xUnit v2 is not supported. Topic `writing-tests` covers what both
frameworks share: locators, reads, expectations, agent steps, and the
`Browser` member.

## Contents

- Project
- The base class
- Attributes
- Saved sessions
- Soft checks and polls
- A complete example
- Run

## Project

```bash
dotnet new install xunit.v3.templates
dotnet new xunit3 -n MyApp.E2E
dotnet add MyApp.E2E package E2E
dotnet add MyApp.E2E package E2E.XUnit.V3
```

The e2e-dotnet sample and tests run on the VSTest runner: their project
references `xunit.v3.mtp-off`, `xunit.runner.visualstudio`, and
`Microsoft.NET.Test.Sdk`. The `--filter` commands below are for that runner.

Put `e2e.config.json` in the project folder. Topic `setup` covers the config
and the browser install.

The session stops on xUnit's cancellation token, so calls on `App`,
`Screen`, `Agent`, `Browser`, and `Expect.That` do not need a token; the
sample turns off the analyzer warning `xUnit1051` that asks for one.
`Expect.Poll` matchers do not see the session: pass
`Xunit.TestContext.Current.CancellationToken` to `ToSatisfyAsync` or
`ToBeAsync` so a cancelled run stops them. Write `Xunit.TestContext` in
full, because `using E2E;` brings in `E2E.TestContext` too.

## The base class

Derive each test class from `E2ETest`:

```csharp
using E2E;
using E2E.XUnit.V3;

public sealed class BillingTests : E2ETest
{
}
```

- `E2ETest` implements `IAsyncLifetime`. `InitializeAsync` starts one engine
  session per test, so every test gets a fresh browser context.
  `DisposeAsync` commits the replay cache from the xUnit result: a pass or
  a skip records verified acts, a failure evicts the unverified ones, and a
  cancelled test leaves the cache alone.
- To run code before or after each test, override `InitializeAsync` or
  `DisposeAsync` and call the base method: first in `InitializeAsync`, last
  in `DisposeAsync`, inside a `finally` so the session is always closed. A
  check there after a failure verifies nothing, so it never lets an act be
  recorded. A constructor runs before the session exists, so do not use
  `App` or `Screen` there.
- The members are protected properties of the base class: `App`, `Agent`,
  `Screen`, `Browser`, `Secrets`, and `Context`. `Expect` and `Credentials`
  are static classes in the `E2E` namespace.
- Settings come from `e2e.config.json`. Override a virtual member to change
  one value for a class: `CreateEngine()`, `CreateModel()`, `CreateJudge()`,
  `CreateAgents()`, `BaseUrl`, `ActionTimeout`, `CacheMode`, and more.

## Attributes

- `[Fact]` marks a test. `[Theory]` rows work too; each row keeps its own
  replay, keyed by the class, the method, and every argument.
- xUnit has no retry, so every test is a first attempt and can replay.
- `[Trait("TestCategory", "RealModel")]` marks a test that calls a real
  model, so CI can skip it.
- `Assert.Skip(reason)` or `Assert.SkipWhen(condition, reason)` skips a test
  from inside its body. A skip reason, static or from a
  running test, is secret-redacted, sanitized, and limited to 8,192 UTF-8 bytes
  before it is reported.
- `Fixme(condition, reason)` on `E2ETest` (upstream's `test.fixme(condition, reason)`) skips the running test from its body, ending the method with a dynamic skip like `Assert.Skip`: the report's skip has the cause `fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank; a false condition skips nothing; a call from an `[E2ESetup]` test fails the setup test with "Fixme() cannot skip a setup test" (the report code is `TEST_FAILED` here, because the host keeps only the exception message; NUnit reports `INVALID_ARGUMENT`), and one outside a test throws `InvalidOperationException` where upstream collects a `COLLECTION_ERROR`.
- `[Fixme]` or `[Fixme("reason")]` on a test method registers it fixme (upstream's `test.fixme(title, fn)`). The test is reported skipped with the cause `fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank, and its body never runs. It starts no session: the host reports it from collection, skipped and selected with `attempts: []` (a filter that also excludes it reports it unselected, with the cause `filtered`), as upstream's registered test is.
- `[Only]` on a test method or a fixture class focuses it (upstream's `test.only` and `describe.only`). When any selected test is focused, the run executes the focused tests, those of a focused fixture and of the fixtures nested in it included, and reports every other test as skipped with the cause `filtered`. A CI run (`CI` set) refuses a focused test with `ONLY_IN_CI`, exit 2.
- `SetTimeout(TimeSpan)` on `E2ETest` (upstream's `test.setTimeout`) changes the running test's timeout from its body or a set-up, counted from the start of the attempt like the configured `timeout`, which it replaces for this attempt; a value shorter than the time already spent times the test out at once. It cuts the calls the test awaits, not a plain `Task.Delay`. A value that is not a positive whole number of milliseconds, or above 2147483647, and code the test left running after it ended fail with `INVALID_ARGUMENT`; a call outside a test throws `InvalidOperationException`. This adapter cannot tell a teardown from the body, so it refuses a call from one only once the attempt has completed.
- `Step(title, body)` on `E2ETest` (upstream's `test.step`) runs `body` as one step named `title` and returns what it returns: `await Step("Log in as test user", async () => { ... })`. Reports show the title, with the calls `body` makes nested under it, and steps nest. The step fails when `body` throws, and the error goes on to the test; catch it to continue. Call it in a test body, a set-up (`InitializeAsync`), or a teardown. The calls inside it are steps of the test like any other: `e2e run --step` stops before each one, not before the `Step` itself, and a failed soft assertion inside does not fail the step. In `report.json` it is a step of kind `step` with the api `test.step` and its title as the label, each step inside it carries its index as `parent`, and step progress carries `identity.parentStepId`; the list reporter shows a running `Step` with the steps inside it, and keeps it in its output only when it failed or an agent step ran inside it. A blank title or no body fails with `INVALID_ARGUMENT`; a call outside a test throws `InvalidOperationException` where upstream collects a `COLLECTION_ERROR`, and one from code the test left running after it ended is cancelled. A `Fixme` inside it skips the test and the step passes; the port does not tell a host's own `Assert.Ignore` (or `Assert.Skip`) from a failure in a step. Not ported: `beforeAll` and `afterAll` hooks (the port has no suite hooks that `E2E` times), and the CTRF and HTML reports.

## Saved sessions

Sign in once, then start other tests signed in (upstream's `test.setup` and
`session`). Mark the sign-in test `[E2ESetup("title", Saves = ["admin"])]`
instead of `[Fact]`; its body signs in, checks that it worked, and calls
`await SaveSessionAsync("admin")` once for each name in `Saves`. Mark a test or
a class `[E2ESession("admin")]` to restore it before the body runs.

```csharp
public sealed class Dashboard : E2ETest
{
    [E2ESetup("authenticate as admin", Saves = ["admin"])]
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

    [Fact]
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
- `E2ETest` orders the setup tests of a class before its other tests (a test
  method orderer, `SetupTestMethodOrderer`); a class with its own
  `[TestMethodOrderer]` loses it. A test that restores a session nobody saved
  yet runs the setup test that declares it first, once per run, so a dependent
  can run alone (upstream's runner schedules the setup before its dependents
  too). The test that needs it runs the setup itself: it builds the setup's
  class with its parameterless constructor and runs the method in a session of
  its own, without the class's own `[SetUp]` and `[TearDown]` methods, and the
  framework then skips that setup test when its turn comes. Exactly one setup
  test of the test assembly must declare the name: with none, or with two, the
  restore fails with `SESSION_UNAVAILABLE`. A setup test that fails skips each
  of its dependents with the cause `setup-failed`. A class-level `[E2ESession]` does not apply to the setup tests of
  that class.
- A restore leaves no page open: call `App.OpenAsync` first. A session holds
  what the browser keeps (cookies, local storage, IndexedDB), not server state.
- An engine without a state capability (`stateCapability` false in
  `report.json`) fails `SaveSessionAsync` and a restore with
  `UNSUPPORTED_CAPABILITY`. `WebEngine` has it.
- A session lasts one run. It is encrypted with a key held in memory and kept
  under `.e2e/sessions/<run id>`. Register `E2ERunFixture` as an assembly
  fixture to delete it when the run ends.
- The package needs `xunit.v3.extensibility.core` 4.0.1 or later (the test
  method orderer).

## Soft checks and polls

- `Expect.Soft(locator)` and `Expect.Soft(screen)` keep each failure on the session. The test body
  runs on, and the test fails when it is disposed, with every failure in
  one message.
- `Expect.Poll(read)` retries a value with `ToBeAsync` or
  `ToSatisfyAsync(predicate)`: `await Expect.Poll(() => ReadCountAsync()).ToSatisfyAsync(count => count > 3);`.

## A complete example

One class per feature, one behavior per test, arrange, act, assert
(topic `writing-tests`, "Structure").

```csharp
using E2E;
using E2E.XUnit.V3;

public sealed class DaritenTests : E2ETest
{
    [Fact]
    public async Task The_dashboard_links_to_the_transaction_register()
    {
        await App.OpenAsync("/");
        await Screen.GetByRole("heading", "Dashboard").WaitForAsync();

        await Screen.GetByRole("link", "Transactions").ClickAsync();

        await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
    }

    [Fact]
    [Trait("TestCategory", "RealModel")]
    public async Task An_agent_filters_the_register_by_category()
    {
        await App.OpenAsync("/transactions");

        await Agent.ActAsync("filter the register to show only the Groceries category");

        await Agent.AssertAsync("every transaction listed is in the Groceries category, or the register says no transactions match");
        await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
    }
}
```

`samples/E2E.XUnit.V3.Sample` in the e2e-dotnet repo holds a longer version
of this class, run against a live demo.

## Run

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~DaritenTests"            # one class
dotnet test --filter "TestCategory!=RealModel"                    # skip model tests
dotnet test --filter "TestCategory=RealModel"                     # only model tests
```

The trait name is the filter name: `[Trait("TestCategory", "RealModel")]`
is what `TestCategory!=RealModel` matches.
