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
- `[Property("E2E.Fail", "reason")]` marks a test as expected to fail (upstream's `test.fail`; `NUnitSessionTestRunner.ExpectedFailureProperty` is the name). A product failure or a timeout passes the test, and the report keeps the reason and the error under `expectedFailure`. A test that passes fails with `UNEXPECTED_PASS`. A group is a fixture: `[Property("E2E.Fail", "reason")]` on a fixture class marks every test in it, as upstream's `describe(title, { fail: 'reason' }, fn)`; `[DescribeFail]` on a fixture class marks every test in it with `expected to fail`, as upstream's `describe.fail(title, fn)` (it takes no reason, as `describe.fail` takes no `fail` option; `[Property("E2E.Fail", "")]` is the same mark). Each test is judged on its own. Any layer that marks marks the test, the innermost reason wins (a blank reason keeps an outer one), and a fixture or test with no property does not unmark. A `[Serial]` fixture marked, inside a marked fixture, or holding a marked test is a `COLLECTION_ERROR` when the tests are collected (a filtered run), and fails the test with it when an unfiltered run starts it, as upstream refuses `fail` on, inside, or around a serial group. A `fail` that is not a reason string (`[Property("E2E.Fail", 1)]`) is a `COLLECTION_ERROR` too. A skip, an infrastructure error, a blocked agent verdict, or a hook failure still decides the test, as unmarked. NUnit has no expected-failure outcome, so the run shows the test as passed. A test can also mark itself in its body or a `[SetUp]` with `Fail(condition, reason)` on `E2ETest` (upstream's `test.fail(condition, reason)`): the body runs on, a blank reason becomes `expected to fail`, and a mark from a `[TearDown]`, a `[E2ESetup]` test, or a `[Serial]` member throws `INVALID_ARGUMENT`. `Fixme(condition, reason)` on `E2ETest` (upstream's `test.fixme(condition, reason)`) skips the running test from its body or a `[SetUp]`, ending the method like `Assert.Ignore`: the report's skip has the cause `fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank; a false condition skips nothing; a failure (a throwing `[TearDown]`, say) outranks the skip, and the attempt keeps the skip beside the failed status (cause `fixme` for `Fixme`, `explicit` for `Assert.Ignore`, whose reason NUnit lists beside the failure); a call from an `[E2ESetup]` test throws `INVALID_ARGUMENT`, and one outside a test throws `InvalidOperationException` where upstream collects a `COLLECTION_ERROR`. The xUnit v3 and TUnit `E2ETest` have `Fixme(condition, reason)` too, ending the test with their own skip.
- `[Fixme]` or `[Fixme("reason")]` on a test method registers it fixme (upstream's `test.fixme(title, fn)`). The test is reported skipped with the cause `fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank, and its body never runs. It starts no session: the host reports it from collection, skipped and selected with `attempts: []` (a filter that also excludes it reports it unselected, with the cause `filtered`), as upstream's registered test is. It is a `COLLECTION_ERROR` on a test of a `[Serial]` fixture, which skips as one unit: skip the fixture with `[Ignore]` instead.
- `[Only]` on a test method or a fixture class focuses it (upstream's `test.only` and `describe.only`). When any selected test is focused, the run executes the focused tests, those of a focused fixture and of the fixtures nested in it included, and reports every other test as skipped with the cause `filtered`. A CI run (`CI` set) refuses a focused test with `ONLY_IN_CI`, exit 2.
- `SetTimeout(TimeSpan)` on `E2ETest` (upstream's `test.setTimeout`) changes the running test's timeout from its body or a `[SetUp]`. The new timeout counts from the start of the attempt, like the configured `timeout`, and replaces it for this attempt (each member of a `[Serial]` group is an attempt of its own, counted from its own start), so a value shorter than the time already spent times the test out at once. As for the configured timeout, it cuts the calls the test awaits (a step, a locator wait), not a plain `Task.Delay`. A value that is not a positive whole number of milliseconds, or one above 2147483647, a call from a `[TearDown]`, a session that `host.OpenSessionAsync` opened (it runs no test; pass `Timeout` there), and code the test left running after it ended fail with `INVALID_ARGUMENT`; a call outside a test throws `InvalidOperationException`. There is no way to ask for no timeout. Not ported: the `beforeAll` form (NUnit owns `[OneTimeSetUp]`).

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
- A setup test gets an early `[Order]`, so it runs before the other tests of
  its own fixture. A test that restores a session nobody saved yet runs the
  setup test that declares it first, once per run, so a dependent can run
  alone (upstream's runner schedules the setup before its dependents too). The
  test that needs it runs the setup itself: it builds the setup's class with
  its parameterless constructor and runs the method in a session of its own,
  without the class's own `[SetUp]` and `[TearDown]` methods, and the
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
  under `.e2e/sessions/<run id>`. Derive an `E2ERunFixture` to delete it when
  the run ends. The secrets the saving test used are redacted in the tests
  that restore it.
- The xUnit v3 adapter has the same attributes (topic `writing-tests-xunit`).

## Soft checks and polls

- `Expect.Soft(locator)` and `Expect.Soft(screen)` record each failure on the NUnit result, as inside
  `Assert.EnterMultipleScope`. The test body runs on, and the test fails
  when it ends, with every failure listed.
- `Expect.That(value, "message")` checks a plain value at once (`ToBeGreaterThanOrEqual`, `ToBeCloseTo`,
  `ToEqual`, and the rest, as upstream's `expect(value)`), and `Expect.Soft(Screen, value)` keeps its failure
  as `Expect.Soft(locator)` does. Topic `writing-tests` lists the matchers.
- `Expect.Poll(read)` retries a value. `E2E.NUnit` adds `ToMatchAsync` with
  any NUnit constraint: `await Expect.Poll(() => ReadCountAsync()).ToMatchAsync(Is.GreaterThan(3));`.
  `Expect.That(block).ToPassAsync()` retries a block of assertions until it stops throwing.
  `ToBeAsync` and `ToSatisfyAsync(predicate)` work too.

## A complete example

One class per feature, one behavior per test, arrange, act, assert
(topic `writing-tests`, "Structure").

```csharp
using E2E;
using E2E.NUnit;

public sealed class DaritenTests : E2ETest
{
    [Test]
    public async Task The_dashboard_links_to_the_transaction_register()
    {
        await App.OpenAsync("/");
        await Screen.GetByRole("heading", "Dashboard").WaitForAsync();

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
