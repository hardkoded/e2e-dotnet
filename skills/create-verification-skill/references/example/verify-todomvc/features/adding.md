# Adding todos

Type a title in the new-todo field and press Enter; the todo joins the list.

## Sub-features

- The `What needs to be done?` field takes a title and clears on Enter.
- Each new todo is unchecked and raises the `N items left` counter.
- Surrounding spaces are trimmed; an empty or spaces-only title adds
  nothing; special characters are kept.

## How to get to it (user POV)

Open `/todomvc`. No account.

## Driving it with e2e

`AddingTodosTests` pins it. Locators: `Screen.GetByRole("textbox", "What
needs to be done?")`, `Screen.GetByTestId("todo-item")`,
`Todo("<title>").GetByRole("checkbox")`, `Screen.GetByText("1 item left")`.
Live: `open_session`, `observe`, type into the field with the verbs, press
Enter, `observe` again.

## Proof

The row is visible and unchecked, the field reads empty, and the counter
says `1 item left`. After a second add it says `2 items left`.

## Charters

```
add-input|todomvc|Starting at /todomvc, add titles that are very long, contain markup such as <b>x</b>, or repeat an existing title; report a row that renders wrong, merges, or breaks the counter
```

## Gotchas

- `1 item left` is singular; `0 items left` and `2 items left` are plural.
  A wrong plural is a finding, the singular form is not.
- The list is empty at every start; there is no seeded todo.
