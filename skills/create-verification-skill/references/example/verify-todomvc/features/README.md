# Feature map

One file per user-facing feature of TodoMVC, from the user's point of
view. Each names the test class that pins it, the locators that matter, the
proof, and its bug-bash charters. The app has one page; every feature is a
part of it. Editing and deleting a todo are in `samples/TodoMvc`
(`EditingTodosTests`, `DeletingTodosTests`); map them when they matter to a
change.

| Feature | Route | Test | Status |
| --- | --- | --- | --- |
| [Adding todos](adding.md) | `/todomvc` | `AddingTodosTests` | tested |
| [Completing todos](completing.md) | `/todomvc` | `CompletingTodosTests` | tested |
| [Filtering todos](filtering.md) | `/todomvc#/active`, `#/completed` | `FilteringTodosTests` | tested |
