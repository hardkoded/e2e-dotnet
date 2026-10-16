# Completing todos

Check a todo off, check every todo at once, or reopen one.

## Sub-features

- A row's `Toggle Todo` checkbox completes or reopens that todo.
- The toggle-all control completes every todo, or reopens every todo when
  all are complete.
- `Clear completed` appears once a todo is complete and removes the
  completed rows.

## How to get to it (user POV)

Open `/todomvc` and add a todo first. No account.

## Driving it with e2e

`CompletingTodosTests` pins it. Name the row first, since every row has the
same checkbox: `Todo("Buy milk").GetByRole("checkbox")`. Other locators:
`Screen.GetByText("0 items left")`,
`Screen.GetByRole("button", "Clear completed")`. The toggle-all control's
accessible name starts with a `❯`, so ask the agent for it with
`Agent.ActAsync("mark all todos as complete with one click")`. Live:
`locate` the row's checkbox before using it.

## Proof

The row's checkbox is checked, the counter drops by one, and `Clear
completed` is visible. Reopening reverses all three.

## Charters

```
complete-toggle-all|todomvc|Starting at /todomvc with three todos, use toggle-all, complete one by hand, then use toggle-all again; report a counter or a checkbox that disagrees with the rows
```

## Gotchas

- `Clear completed` is hidden while no todo is complete. Its absence on a
  fresh list is not a bug.
- The strike-through on a completed title is style. The model judges the
  accessibility tree, so check it with a locator on the style, not a
  judgment.
