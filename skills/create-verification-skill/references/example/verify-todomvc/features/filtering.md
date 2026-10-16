# Filtering todos

Show all, active, or completed todos from the footer.

## Sub-features

- The footer links `All`, `Active`, and `Completed` change the list and the
  URL hash (`#/`, `#/active`, `#/completed`).
- The selected filter is highlighted.

## How to get to it (user POV)

Open `/todomvc`, add todos, and complete one. The footer shows once the
list has a todo. No account.

## Driving it with e2e

`FilteringTodosTests` pins it. Locators: `Todo("<title>")` for a row,
`Todo("<title>")` with `ToBeHiddenAsync` for a filtered-out row, and
`Browser.UrlAsync()` polled for the hash. The footer filters are list items
too, so find rows by the `todo-item` test id, never by `listitem`. Live:
click the footer link, `observe`, and read the URL.

## Proof

After `Active`, the URL ends with `#/active`, the active rows are visible,
and the completed row is hidden. `Completed` shows the reverse.

## Charters

```
filter-navigation|todomvc|Starting at /todomvc with one active and one completed todo, switch filters, reload the page on each, and use the browser Back button; report a list that disagrees with the URL hash
```

## Gotchas

- A filter shows only matching rows; the counter still counts all active
  todos. A counter that follows the filter would be a finding.
- Reloading on `#/active` keeps the filter; the list itself comes from
  storage.
