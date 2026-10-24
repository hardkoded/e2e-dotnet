# Driving the app over MCP

`e2e mcp` serves an e2e project to a coding agent over MCP (stdio). The
coding agent will drive the app the way the testing agent does: look at a
screen before writing a test, check a locator before committing to it.
Running tests and reading a failed run stay with `dotnet test`.

**In the .NET port** a session drives the one target of `e2e.config.json`. A
`web` target runs in a Chromium browser. An `ios` or `android` target with
`app.bundleId` or `app.appPath` runs on a local simulator or emulator through
`MobileEngine`: it needs Node 22.12 or newer and a booted device, and
`E2E_MOBILE_DEVICE` names one. With `E2E_MOBILE_PROVIDER=limrun` the session
leases a hosted Limrun device instead (set `LIMRUN_API_KEY`); with
`E2E_MOBILE_PROVIDER=devflow` it attaches to the MAUI app you launched (see
[mobile.md](mobile.md)). `open_session` leases the device, and the lease bills
until `close_session` releases it: close sessions you no longer need. The idle
timeout, the four-hour limit, and a server shutdown release it too. A device
session has no `navigate` tool. A `windows` target with `app.appPath` (the executable to
launch) or `app.bundleId` (the process name of a running app) runs on
`WinAppEngine` through Windows UI Automation, on Windows only, and needs an
unlocked desktop for keys and clicks (see [windows.md](windows.md)); it launches
the app before the first observation and has no `navigate` tool either. The catalog is the testing agent's own actions plus `locate`,
`test_lines`, and `screenshot`. Not ported: recording a video, the project's own tools,
and the tap, hover, and type at a point. `e2e init` does not exist, so
register the server yourself (below).

## Contents

- Setup
- Tools
- The catalog
- Workflow
- Rules

## Setup

The server ships with the `E2E.Cli` tool (`dotnet tool install --global E2E.Cli`).
Register it with the client:

```bash
claude mcp add e2e -- e2e mcp   # Claude Code
```

Or declare it in the client's project config (`.mcp.json` for Claude Code,
`.cursor/mcp.json` for Cursor, `.vscode/mcp.json` for VS Code):

```json
{ "mcpServers": { "e2e": { "command": "e2e", "args": ["mcp"] } } }
```

In a clone of the e2e-dotnet repo, run it as
`dotnet run --project src/E2E.Cli -- mcp`.

Sessions run on the project's config (`e2e.config.json`): in a project
without one, `open_session` fails with `CONFIG_NOT_FOUND`, so set it up
first (topic `setup`).

Flags: `--config <path>` names the default config file, `--target <name>`
fixes the target every session opens on, `--headed` shows the browser in
sessions that do not set `headed` (sessions are headless by default),
`--max-sessions <n>` sets how many sessions may be open at once (default 4,
1 through 16). A bad value exits with code 2. The server exits with 0 when
the client disconnects or on Ctrl+C or `SIGTERM`, and with 1 on an
unexpected error. Stdout carries only the protocol; diagnostics go to
stderr.

## Tools

Four tools, all on the server registered as `e2e`; everything a session can
do is a catalog behind `call`. Clients show them with the server name, such
as `mcp__e2e__open_session` in Claude Code; when another MCP server offers a
tool with the same name, use the `e2e` server's.

| Tool | Does |
| --- | --- |
| `open_session` | Loads the config (`config` names another file; default the nearest `e2e.config.json`), boots the browser or device, opens the app (its URL, or launches a device's pinned app; it installs no build, so a local device needs `app.bundleId`), and returns the session id, the catalog, and the first observation. `target` must name the config's target when given. `headed: true` shows the browser when the user wants to watch. |
| `tools` | The catalog: one line per tool with its argument names (`?` marks optional), the first sentence of its description, and `[read-only]` where it changes nothing. `tools {tool}` shows the full description and the JSON Schema of its arguments. |
| `call` | Runs one catalog tool: `call {tool: "tap", args: {target: "e12"}}`, where `e12` is the `ref=` of a node in the newest observation. Arguments are checked against the tool's schema first; a wrong one fails with `INVALID_ARGUMENT` naming the field. |
| `close_session` | Ends the session and disposes the browser. With no session open and no `session` argument, it says `No session is open.` |

Every fixed tool takes only the arguments it declares. An unknown one fails
before anything runs, with `Invalid arguments for tool <name>: Unrecognized
key: "<key>"`.

Resources: `e2e://guide` is the skill overview, and `e2e://guide/<topic>`
holds one topic (`e2e guide <topic>` prints the same text). An unknown topic fails with
`UNKNOWN_TOPIC`.

## The catalog

`observe` shows the whole screen, one node per line with its `[ref=...]`. Every
action returns the screen after it. What an engine lacks is missing from its
catalog: `scroll` and `scroll_to` need scrolling, `back` needs history,
`screenshot` needs the web engine, and `type_secret` appears only when the
config declares a `secrets` entry.

| Tool | Does |
| --- | --- |
| `observe` | The whole current screen |
| `tap`, `double_tap`, `check`, `uncheck`, `clear` `{target}` | Act on one node. On an engine that declares `TapModifiers` (the web engine), `tap` and `double_tap` also take an optional `modifiers` list (`Shift`, `Control`, `Alt`, `Meta`, `ControlOrMeta`) to hold keys for the click, as a Shift-click extends a selection |
| `type {target, text}` | Replace the text in a field |
| `type_secret {target, secret}` | Fill a configured secret into an editable field by name; you never see the value |
| `press {key, target?}` | Press a key on a node, or on whatever has focus |
| `select {target, value}` | Choose an option |
| `scroll {direction, times?, target?}`, `scroll_to {target?, text?, direction?}` | Move the viewport, or page to a node |
| `navigate {url}` | Open a path or an http or https URL. Any other scheme fails with `POLICY_DENIED` |
| `back` | One step back in the history |
| `screenshot` | The viewport as an image. After a secret was filled it answers `PIXEL_TAINTED` and attaches nothing |
| `locate {role? name? text? label? placeholder? testId? exact?}` | Try a locator; see the loop below |
| `test_lines` | Every test line the session's actions printed, in order, as a draft test body; see below |

A failed action is an error result that starts with the tool, the node, and
the code: `tap n9999 failed: NOT_FOUND: ...`.

### Test lines

Each action answers with the line that repeats it in a test, after the
first line of its result:

```text
tapped button "Sign in"
Test line: await Screen.GetByRole("button", "Sign in").TapAsync();
```

`test_lines` returns every line so far, in order, as a draft test body that
starts with `await App.OpenAsync();` for the session's own opening. Paste it
into a test, then add the assertions.

- The locator comes from the node as the newest observation listed it,
  before the action runs. Role and name, label, placeholder, text, and test
  id are tried in that order. The first that matches the node and nothing
  else on the live screen wins, as `locate` would report it.
- A locator that also matches twins is narrowed: to the nodes that render
  (`new RoleOptions { Visible = true }`), then under a container
  (`GetByRole("listitem").Filter("Offer B").GetByRole("button", "Reserve")`),
  then by position (`.Nth(1)`, with a comment saying so). A position is used
  only when the live twins are the ones the observation listed, each in place
  or all moved together.
- At most 10 locators are tried per node.
- Each locator resolves through the engine's `LocateAsync` (2 s each) when
  the engine has one, so the node ids you hold stay valid; otherwise on a
  fresh observation.
- When nothing reaches the node alone, the result says `No test line:` and
  why, and the draft shows the action as a `//` comment. A line that would
  fail is never printed. A failed action prints and records no line.
- A fill names `Secrets.Get(name)`, never a value.
- A `navigate` to a URL on the app's origin is written as its path
  (`App.OpenAsync("/settings")`); `back` is `App.BackAsync()`.

| Action | Line |
| --- | --- |
| Opening the session | `App.OpenAsync()` |
| `tap`, `double_tap` | `TapAsync()`, `DoubleTapAsync()` |
| `type` | `FillAsync(text)` |
| `type_secret` | `FillAsync(Secrets.Get(name))` |
| `press` on a node | `PressAsync(key)` |
| `select`, `check`, `uncheck`, `clear` | `SelectOptionAsync(value)`, `CheckAsync()`, `UncheckAsync()`, `ClearAsync()` |
| `scroll_to` a node | `ScrollIntoViewAsync()` |
| `navigate`, `back` | `App.OpenAsync(url)`, `App.BackAsync()` |

No line: `type` or `press` with no target, `tap` or `double_tap` with
`modifiers` (a locator tap takes none), `scroll`, and `scroll_to` a text.

## Workflow

The loop is:

1. `open_session`, then `call {tool: "observe"}` and act until the screen you
   want to test is in front of you. Node refs are valid only for the newest
   observation. Each action prints the test line that repeats it: copy it as
   you go, or take them all from `test_lines`.
2. `call {tool: "locate", args: {...}}` for each locator no action printed,
   such as an assertion's target.
   One match: use the printed `Screen.GetByRole(...)` call. Zero or several:
   adjust before writing the test; the same failure would hit the test as
   `LOCATOR_NOT_FOUND` or `STRICT_MODE`. A role query's `name` matches
   the accessible name only, while `observe` shows a `status`, `alert`, or
   `listitem` by its text: `status "Saved."` on an unlabelled status is its
   text, so match it with `Screen.GetByRole("status").Filter("Saved.")`
   or `Screen.GetByText`.
3. Write an `E2ETest` class (topic `writing-tests`). Deterministic steps where
   you saw exact names; `Agent.ActAsync` where the flow varies.
4. Run only that test from the shell:
   `dotnet test --filter "FullyQualifiedName~<Class>.<Method>"` (SKILL.md,
   "Rerunning a test while you fix it"). On a failure, fix from the exception
   and the trace page (topic `debugging`), then rerun it the same way. To see a screen the evidence leaves out, go
   straight to it instead of walking the flow again from the start:
   `navigate` to its path, then `observe` and `locate`. On a device, a session
   starts on the launch screen of the pinned app, launched fresh. A second
   session on a device target fails with `ENGINE_IN_USE` until the first
   closes.
5. Leave the session open through those runs, on the screen under test,
   wherever Rules lets a run go beside it, so step 4's live look needs no
   new session. `observe` does not reload: `navigate` even to the screen
   the session is on when the run or a fix may have changed what it shows.
   A live look's actions print test lines too: copy a fixed line from its
   action, since `test_lines` also lists the looks after the first walk.
   `close_session` once the test passes.

## Rules

- A session ends when you close it, after 30 minutes without a call, or after
  4 hours. A secret value never leaves the server: every output shows it as
  `<secret:name>`.
- Nothing a session does is recorded as a test or into the replay cache, and
  it writes no trace page, and `e2e mcp` takes no `--trace`, `--video`, `--debug`,
  or `--ai-trace`, as upstream. A session is for looking and trying; the test is
  what you write afterwards.
- On the web, a session can stay open while `dotnet test` runs: the port has
  no `app.command`, so the app runs elsewhere and both use it. On mobile,
  close the session before each run: a session and a run on one device fight
  over it. It can stay open only on a device no run uses.
- Parallel agents (subagents) share one server: each opens its own session
  and passes its session id to every `tools`, `call`, and `close_session`.
  A call may leave `session` out only while one session is open.
- `NO_SESSION`: call `open_session` first, or the session named has ended
  (the message says why). `SESSION_REQUIRED`: several sessions are open;
  pass `session`. `SESSION_OPEN`: all slots are taken (`--max-sessions`).
  `CONFIG_IN_USE`: sessions open at once share one config. `UNKNOWN_TOOL`: the name is not in this session's catalog;
  the message lists what is.
