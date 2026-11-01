# Writing tests with NUnit

The `E2E.NUnit` package holds `E2E.NUnit.E2ETest`, an NUnit base class.
Topic `writing-tests` covers what both frameworks share: locators, reads,
expectations, agent steps, and the `Browser` member.

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
dotnet new nunit -n MyApp.E2E
dotnet add MyApp.E2E package E2E
dotnet add MyApp.E2E package E2E.NUnit
```

Put `e2e.config.json` in the project folder. Topic `setup` covers the config
and the browser install.

## The base class

Derive each test class from `E2ETest`:

```csharp
using E2E;
using E2E.NUnit;

public sealed class BillingTests : E2ETest
{
}
```

- Its `[SetUp]` starts one engine session per test, so every test gets a
  fresh browser context. Its `[TearDown]` commits the replay cache from the
  NUnit result: a pass or a skip records verified acts, a failure evicts
  the unverified ones, and a cancelled test leaves the cache alone.
- Your own `[SetUp]` runs after the base class's, so the session is ready
  there. Your `[TearDown]` runs before the base class's. A check there after
  a failure verifies nothing, so it never lets an act be recorded.
- The members are protected properties of the base class: `App`, `Agent`,
  `Screen`, `Browser`, `Secrets`, and `Context`. `Expect` and `Credentials`
  are static classes in the `E2E` namespace.
- Settings come from `e2e.config.json`. Override a virtual member to change
  one value for a class: `CreateEngine()`, `CreateModel()`, `CreateJudge()`,
  `CreateAgents()`, `BaseUrl`, `ActionTimeout`, `CacheMode`, and more.

## Attributes

- `[Test]` marks a test; `[TestCase]` rows work too.
- `[Retry(n)]`: the first attempt can replay the cache; later attempts run
  live and still record. Each `[Repeat]` iteration is a first attempt.
- `[Category("RealModel")]` marks a test that calls a real model, so CI can
  skip it.
- `Assert.Ignore(reason)` or `Assume.That(...)` skips a test from inside its body.

## Saved sessions

Sign in once, then start other tests signed in (upstream's `test.setup` and
`session`). Mark the sign-in test `[E2ESetup("title", Saves = ["admin"])]`
instead of `[Test]`; its body signs in, checks that it worked, and calls
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
        await Expect.That(Browser).ToHaveUrlAsync("/dashboard");
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
- NUnit picks the order, not the dependents. A setup test gets an early
  `[Order]`, so it runs before the tests of its fixture and before the other
  fixtures of its namespace. Give a fixture that depends on a session a later
  `[Order]` when it sits in another namespace. A test that restores a session
  nobody saved fails with `SESSION_UNAVAILABLE`. Unlike upstream's runner,
  NUnit runs a setup test even when no selected test needs its session.
- A restore leaves no page open: call `App.OpenAsync` first. A session holds
  what the browser keeps (cookies, local storage, IndexedDB), not server state.
- An engine without a state capability (`stateCapability` false in
  `report.json`) fails `SaveSessionAsync` and a restore with
  `UNSUPPORTED_CAPABILITY`. `WebEngine` has it.
- A session lasts one run. It is encrypted with a key held in memory and kept
  under `.e2e/sessions/<run id>`. Derive an `E2ERunFixture` to delete it when
  the run ends. The secrets the saving test used are redacted in the tests
  that restore it.
- Only the NUnit adapter has the attributes so far.

## Soft checks and polls

- `Expect.Soft(locator)` and `Expect.Soft(screen)` record each failure on the NUnit result, as inside
  `Assert.EnterMultipleScope`. The test body runs on, and the test fails
  when it ends, with every failure listed.
- `Expect.Poll(read)` retries a value. `E2E.NUnit` adds `ToMatchAsync` with
  any NUnit constraint: `await Expect.Poll(() => ReadCountAsync()).ToMatchAsync(Is.GreaterThan(3));`.
  `ToBeAsync` and `ToSatisfyAsync(predicate)` work too.

## A complete example

```csharp
using E2E;
using E2E.NUnit;

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
        await Agent.AssertAsync("every transaction listed is in the Groceries category, or the register says no transactions match");
        await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
    }
}
```

`samples/E2E.Sample` in the e2e-dotnet repo holds a longer version of this
class, run against a live demo.

## Run

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~DaritenTests"            # one class
dotnet test --filter "Name=The_dashboard_links_to_the_transaction_register"
dotnet test --filter "TestCategory!=RealModel"                    # skip model tests
dotnet test --filter "TestCategory=RealModel"                     # only model tests
```
