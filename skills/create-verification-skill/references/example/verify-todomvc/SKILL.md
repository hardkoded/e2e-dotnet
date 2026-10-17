---
name: verify-todomvc
description: Start, check, drive, prove, and bug bash the TodoMVC demo web app (samples/TodoMvc) with the .NET port of e2e. Use before claiming a TodoMVC change works, when asked to verify or screenshot a page, or to bug bash it.
---

# Verify TodoMVC

Read `.agents/skills/e2e-dotnet/SKILL.md` first. This skill only says what
is true of this app. The config is `samples/TodoMvc/e2e.config.json` (one
web target, `todomvc`); the tests are the NUnit classes in `samples/TodoMvc`
that derive from `TodoMvcTest`. The same steps hold for an xUnit v3 project
(topic `writing-tests-xunit`).

## Launch

Nothing to start. The target's `app.url` is the hosted demo,
`https://demo.playwright.dev/todomvc`. Readiness is the page answering:
`curl -sI https://demo.playwright.dev/todomvc` returns `200`. The app needs
no env, seeds, or database; the list lives in the browser's storage and
every test starts in a fresh browser context, so it starts empty. The real
model tests use the GitHub Copilot login (`e2e login github-copilot`).

## Doctor

```bash
dotnet test --project samples/TodoMvc --filter "FullyQualifiedName~AddingTodosTests.Adds_a_single_todo"   # opens the app, adds a todo; exit 0
```

The sample has no model-free test, so its Doctor uses the model; a project
of your own should add a smoke test that needs none. `APP_UNREACHABLE` is the network or the demo being down. `LOGIN_REQUIRED`
or `MODEL_PROVIDER_FAILED` is the model login (topic `debugging`). A
`LOCATOR_NOT_FOUND` on the `todo-item` test id is the app itself. Run
Doctor first whenever anything looks off.

## Drive

Live, over the registered `e2e mcp` server: `open_session`, then
`observe`, the verbs, and `locate` before writing any locator (topic
`mcp`). The catalog offers no credentials and no project tools. Scripted:
a class under `samples/TodoMvc`, `Screen.GetByTestId("todo-item")` for rows
(the footer filters are list items too), `Agent.ActAsync` for a flow that
varies. Every row has the same `Toggle Todo` checkbox and Delete button, so
name the row first: `Todo("Task 2").GetByRole("button", "Delete")`.

## Evidence

The proof is the `dotnet test` result: add `--logger
"trx;LogFileName=todomvc.trx"` and keep the `.trx` file (topic `running`).
The port writes no report, trace page, or video. A proof drives the page
the way a user does, the typed text, the checkbox, the footer filter, and
pins the action and the state after it: the row, the `N items left`
counter, the URL hash. Side effects: the list is the browser's local
storage (read with `Browser.EvaluateAsync<string>`); the server keeps
nothing. `.e2e/cache` holds the replay of each verified step; do not edit
it.

## Bug bash

The bug bash fan-out is not ported, so a bash is a live MCP walk of a feature's
charter from [features/](features/), one H2 per feature, in the
`slug|target|charter` line format. A finding is a bug once a repro test
fails with `ASSERTION_FAILED` on the assertion that encodes it (topic
`bug-bash`). Give each repro its own data with `Values.Unique(...)`. The
demo is hosted and shared, but the list is per browser context, so several
walks do not collide.

## Cleanup

`close_session` stops the browser a session started. There is no app to
stop. Delete a repro test whose finding was rejected; keep the `.trx`
files and `.e2e/cache`, which are the proof.

## Helpers

None. `dotnet test` and the `e2e` tool are the helpers.
