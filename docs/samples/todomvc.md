---
title: TodoMVC
---

# TodoMVC sample

Playwright's [`examples/todomvc`](https://github.com/microsoft/playwright/tree/main/examples/todomvc) suite against the [TodoMVC demo](https://demo.playwright.dev/todomvc), with the agent performing the actions. It uses the `E2E` and `E2E.NUnit` NuGet packages, so it shows a project that references the published packages.

- Source: [`samples/TodoMvc`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/TodoMvc).
- Platform: web. Every test is in the `RealModel` category.
- Test classes: adding, completing, deleting, editing, and filtering todos, on a shared `TodoMvcTest` base class.

## Playwright and e2e side by side

Playwright, `should-save-edit-on-blur`:

```ts
await page.getByRole('textbox', { name: 'What needs to be done?' }).fill('Call dentist');
await page.getByRole('textbox', { name: 'What needs to be done?' }).press('Enter');
await page.getByTestId('todo-title').dblclick();
await page.getByRole('textbox', { name: 'Edit' }).fill('Schedule dentist appointment');
await page.getByRole('heading', { name: 'todos' }).click();
await expect(page.getByText('Schedule dentist appointment')).toBeVisible();
```

e2e, `Saves_an_edit_when_the_field_loses_focus`:

```csharp
await AddTodosAsync("Call dentist");
await Agent.ActAsync(
    "rename the todo Call dentist to Schedule dentist appointment. Save it by clicking the todos heading, not by pressing Enter");
await Expect.That(Todo("Schedule dentist appointment")).ToBeVisibleAsync();
```

The test describes what the user does. It does not say how to find the edit box or that a double-click opens it. When the markup changes, the agent finds the new controls, and the cache records the new path. The checks stay exact: `Expect.That` checks the counter, the checkboxes, and the row count with no model call.

When a step acts on one row of many, use a locator, because the replay cache records a control by role and name only: `Todo("Task 2").GetByRole("button", "Delete")`.

| | Playwright | e2e |
| --- | --- | --- |
| Tests | 23 | 20 |
| Lines of test code | 625 | 310 |

## Run it

```bash
dotnet tool install --global E2E.Cli
e2e login github-copilot
dotnet test --project samples/TodoMvc
```

On one machine, the first run of all 20 tests took about 4 minutes. The next run replayed every step and took 17 to 32 seconds. `AssertAsync` calls the model on every run, so this sample avoids it. By default the model judges the accessibility tree, not pixels, so a style such as strike-through needs a locator check or `Vision = VisionMode.On`.
