# Writing tests

## A complete file

```csharp
using E2E;
using E2E.NUnit;

public sealed class TodoTests : E2ETest
{
    [SetUp]
    public Task OpenTodos() => App.OpenAsync("/todos");

    [Test]
    [Category("RealModel")]
    public async Task Adds_and_completes_a_todo()
    {
        await Agent.ActAsync("add a todo with the given title", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["title"] = "Write the release notes" },
        });
        await Expect.That(Screen.GetByRole("listitem")).ToHaveCountAsync(1);

        await Agent.ActAsync("mark the todo as done");
        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("0 remaining");
    }

    [Test]
    public async Task Ignores_an_empty_submission()
    {
        // An exact interaction: the empty submit is the point of the test.
        await Screen.GetByRole("button", "Add").ClickAsync();
        await Expect.That(Screen.GetByRole("listitem")).ToHaveCountAsync(0);
    }
}
```

The agent does the flow; `Expect` pins what must be true after each goal,
and that check lets the replay cache rerun the step later. `Screen` actions
are for exact interactions and values. Every test starts from a fresh
browser context, so it opens a page first.

## Locators

- Prefer `GetByRole(role, name)` with the accessible name the app renders.
  Then `GetByLabel`, `GetByPlaceholder`, `GetByText`, and `GetByTestId`.
- Text matches are exact by default. Pass `exact: false` for a substring.
- A locator resolves when it is used. Actions wait for the node to be
  ready, and `Expect.That` retries until its timeout.
- A locator that matches two nodes fails with `STRICT_MODE`. Narrow it with
  `Filter(...)`, a more exact name, or `First()`, `Last()`, `Nth(i)` when the
  order is the point. No match fails with `LOCATOR_NOT_FOUND`.

## Steps

- One goal per `Agent.ActAsync`. Write the outcome, not the clicks.
- Follow every act with `Expect.That`, `Agent.AssertAsync`, or
  `Agent.WaitForAsync`. Only a verified act is recorded for replay.
- Use `Expect.That` when the check is exact; it needs no model.
- Pass data in `Params`, never in the instruction text. A password is a
  `Secret` from `Secrets.Get(name)`; the model never sees its value.
- Mark a test that calls a real model with `[Category("RealModel")]`.
