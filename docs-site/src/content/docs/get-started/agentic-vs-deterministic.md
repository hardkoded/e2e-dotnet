---
title: Agentic or deterministic
description: Choose agentic or deterministic actions and assertions for your end-to-end tests.
sidebar:
  label: Agentic or deterministic
---

Agentic testing lets you describe a user's intention and the expected
result in natural language. The agent can choose the actions to perform
and judge whether the screen meets your expectations. Your test focuses
on the outcome, with no selectors to maintain as the UI changes.

Deterministic testing specifies the actions and checks in code. Both
approaches can test user outcomes, and e2e lets you mix them in one test.

## When to use agentic tests?

Use agentic tests when UI changes keep forcing you to update selectors
in your tests. They also suit flows that take many interactions but are
easy to describe in plain English, such as filling out and submitting a form.

`Agent.ActAsync` lets you describe a user intention, such as applying a
discount code. The agent figures out which controls to use and what to do.
`Agent.AssertAsync` checks whether the screen shows the result you describe,
such as a discount applied to the total.

For example, inside a test on `E2ETest`, which provides `App` and `Agent`:

```csharp
await App.OpenAsync("/checkout");
await Agent.ActAsync("apply the discount code SUMMER");
await Agent.AssertAsync("the checkout shows code SUMMER applied with a 10% discount");
```

You don't need to name the discount field, the button that applies it,
or the element that shows the result.

Tests tied to CSS classes or test IDs need edits when those identifiers
change. Move a button or redesign a form, and the agent can still carry
out the user's intention using what's on screen.

Agentic tests enable monitoring with real data on staging or production
without seeding deterministic data, because agents adapt to your app.
Use a test account there: the model sees what's on screen. See
[Core concepts](/get-started/core-concepts/) for what a judgment reads and
how `Secret` values are kept from a model.

For a complex journey, write a sequence of goals in plain English. Keep
each goal to one outcome and check it before continuing. A verified
`ActAsync` step is recorded, so a short goal per step replays cleanly (see
[Caching & self-healing](/features/caching/)).

## When to use agentic assertions?

Agentic assertions are useful when several wordings or layouts would
satisfy the same requirement. For example, a validation message can
explain what's wrong without matching one exact sentence:

```csharp
await Agent.AssertAsync("the error message explains that an email address is required");
```

For a visual check, set `Vision = VisionMode.On` so the model also sees a screenshot:

```csharp
await Agent.AssertAsync("the chart trends upward", new AssertOptions { Vision = VisionMode.On });
```

The agent's observation shows a frame from another site, such as a hosted card field, as an `iframe` node with no content. To judge that content, use `Vision = VisionMode.On` (or `Only`), and reach it with script steps through `Browser.FrameLocator`. See [Working with the browser](/features/browser/#reach-into-an-iframe).

`Agent.AssertAsync` checks the current screen and fails if the statement is
false or the evidence is insufficient. Use `Agent.WaitForAsync` when the UI
still needs time to reach the expected state. See
[Core concepts](/get-started/core-concepts/) for how a judgment reads the screen.

## When to use deterministic tests?

Use locators for short, stable flows where you already know which controls
to use. You specify each field and button in code, as in this discount test:

```csharp
await App.OpenAsync("/checkout");
await Screen.GetByLabel("Discount code").FillAsync("SUMMER");
await Screen.GetByRole("button", "Apply").TapAsync();
await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("10% off");
```

These actions and checks make no model calls. Prefer roles and labels so
the test refers to controls the user recognizes. Deterministic tests don't
have to depend on CSS classes or test IDs.

Use deterministic assertions when you can express the expected result
as an exact value, count, or state. They check it without model judgment.
If a validation message must use specific wording, check that text with
`Expect`.

## Combine agentic with deterministic tests

Choose actions and assertions independently. You can let the agent find
its way through a changing UI, then check an exact value with `Expect`:

```csharp
await Agent.ActAsync("apply the discount code SUMMER");
await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("10% off");
```

You can also use locators to fill and submit a form, then use
`Agent.AssertAsync` to check whether its validation message explains the problem.

Adaptation follows the goal you write. A vague instruction can allow a
result you didn't intend. If a specific path matters, name it in the goal
or use locators for those actions. An `Expect` after `Agent.ActAsync` catches
a goal that passed the wrong way.

Model-driven actions and assertions add latency and cost, and can make
mistakes. The [replay cache](/features/caching/) can repeat recorded
`Agent.ActAsync` actions without model calls. Assertions run again on every
test run. When a recording no longer matches, the agent can adapt to the
changed UI unless `cache.strict` is enabled. See
[Self-healing tests](/features/caching/#self-healing-tests) for how this works.

## Next

- [Core concepts](/get-started/core-concepts/): see a test that mixes agent steps and locator checks, and how config names the app and the model.
- [Self-healing tests](/features/caching/#self-healing-tests): see how an agent recovers when a recorded flow changes.
