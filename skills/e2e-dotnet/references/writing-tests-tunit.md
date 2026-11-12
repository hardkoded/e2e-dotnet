# Writing tests with TUnit

The `E2E.TUnit` package holds `E2E.TUnit.E2ETest`, a TUnit base class. Topic
`writing-tests` covers what every framework shares: locators, reads,
expectations, agent steps, and the `Browser` member.

## Contents

- Project
- The base class
- Soft checks and polls
- A complete example
- Run
- Not supported yet

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
dotnet test -- --treenode-filter "/*/*/*/*[Category!=RealModel]"   # skip the real-model tests
dotnet test -- --treenode-filter "/*/*/*/*[Category=RealModel]"    # only the real-model tests
```

## Not supported yet

The TUnit package has no `E2ERunFixture` (so no reporters and no app-command
shutdown at the end of a run, and no `--changed-since` or `--last-failed`
selection), no `[E2ESetup]` or `[E2ESession]` saved sessions, and no mobile or
Windows base classes (`MobileAppTest`, `WinAppTest`). Use NUnit or xUnit v3
for those.
