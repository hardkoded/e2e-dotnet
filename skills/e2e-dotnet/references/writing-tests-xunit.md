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
  from inside its body.

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
- xUnit picks the order, not the dependents. `E2ETest` orders the setup tests
  of a class before its other tests (a test method orderer,
  `SetupTestMethodOrderer`); that is all it does. A class with its own
  `[TestMethodOrderer]` loses it. Put the setup test and the tests that restore
  its sessions in one class: xUnit runs classes in parallel, so a test in
  another class can restore a session before it is saved. A class-level
  `[E2ESession]` does not apply to the setup tests of that class. A test that
  restores a session nobody saved yet fails with `SESSION_UNAVAILABLE`. Unlike
  upstream's runner, xUnit runs a setup test even when no selected test needs
  its session.
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

```csharp
using E2E;
using E2E.XUnit.V3;

public sealed class DaritenTests : E2ETest
{
    [Fact]
    public async Task The_dashboard_links_to_the_transaction_register()
    {
        await App.OpenAsync("/");
        await Expect.That(Screen.GetByRole("heading", "Dashboard")).ToBeVisibleAsync();

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
