# Driving the app over MCP

`e2e mcp` serves an e2e project to a coding agent over MCP (stdio). The
coding agent will drive the app the way the testing agent does, write a test
as it walks the flow, and check its locators before writing them.
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
timeout, the four-hour limit, and a server shutdown release it too, within the
config's `cleanupTimeout`. A release that fails or outlives that budget is reported
as a `Cleanup:` line in the result, and the device may stay leased and billed until
the provider's own time limit ends the lease. A device
session has no `navigate` tool. A `windows` target with `app.appPath` (the executable to
launch) or `app.bundleId` (the process name of a running app) runs on
`WinAppEngine` through Windows UI Automation, on Windows only, and needs an
unlocked desktop for keys and clicks (see [windows.md](windows.md)); it launches
the app before the first observation and has no `navigate` tool either. The catalog is the testing agent's own actions plus `locate`,
`test_lines`, and `screenshot`. Not ported: recording a video, the project's own tools,
and the tap, hover, and type at a point. `e2e init --yes` registers the
server in `.mcp.json` and `.cursor/mcp.json`; to do it yourself, see below.

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
first (topic `setup`). `open_session {config}` and `--config` take an
absolute path or one relative to the server's working directory (`cwd` in the
client's MCP config); a missing file fails `CONFIG_NOT_FOUND` naming the
resolved path and that directory.

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
| `open_session` | Loads the config (`config` names another file; default the nearest `e2e.config.json`), boots the browser or device, opens the app (its URL, or launches a device's pinned app; it installs no build, so a local device needs `app.bundleId`), and returns the session id, the catalog, and the first observation. Its summary names the app URL, a port assigned for `:0`, and the app command's readiness URL and `env` names; e2e starts nothing else, so a database the app uses runs outside it. The config reads the MCP server's environment, not your shell's. `target` must name the config's target when given. `headed: true` shows the browser when the user wants to watch. `test` (`Namespace.Class.Method`) runs that one NUnit test of the config's `testAssembly` first, as `dotnet test` would (set-up, body, tear-down of the test's own hooks, replay cache as configured), and hands the session over on the screen where it passed or failed: the opening text leads with the outcome, the error code and message. To fix a failing test, open it this way, look at the screen, and put the lines you ran in the test; close the session, rebuild the test project (restart the server for the new build), and open it again. Build the test project first. The test's own steps print no test line. |
| `tools` | The catalog: one line per tool with its argument names (`?` marks optional), the first sentence of its description, and `[read-only]` where it changes nothing. `tools {tool}` shows the full description and the JSON Schema of its arguments. |
| `call` | Runs one catalog tool: `call {tool: "tap", args: {target: "e12"}}`, where `e12` is the `ref=` of a node in the newest observation. Arguments are checked against the tool's schema first; a wrong one fails with `INVALID_ARGUMENT` naming the field. |
| `close_session` | Ends the session, disposes the browser or device engine, and releases a hosted device the session leased, each within the config's `cleanupTimeout`. A cleanup that fails or times out is a `Cleanup:` line in the result, with the error's code when it has one (`CLEANUP_TIMEOUT` for a release that outlived the budget). With no session open and no `session` argument, it says `No session is open.` |

Every fixed tool takes only the arguments it declares. An unknown one fails
before anything runs, with `Invalid arguments for tool <name>: Unrecognized
key: "<key>"`.

A queued call cancelled by the client does not execute when its turn arrives.

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
| `type_secret {target, secret}` | Fill a configured secret into an editable field by name; you never see the value, and its test line names the secret (`FillAsync(Secrets.Get(name))`) |
| `press {key, target?}` | Press a key on a node, or on whatever has focus |
| `select {target, value}` | Choose an option |
| `scroll {direction, times?, target?}`, `scroll_to {target?, text?, direction?}` | Move the viewport, or page to a node |
| `navigate {url}` | Open a path or an http or https URL. Any other scheme fails with `POLICY_DENIED` |
| `back` | One step back in the history |
| `screenshot {path?}` | The viewport as an image. It masks secure fields only, after a secret fill too. With `path` (a `.png` inside the project, relative to its root, not hidden or under a hidden directory), also saves a full-resolution masked capture there and returns its absolute path; any other path is `POLICY_DENIED` |
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

1. Before `open_session`, create the target's test class with its usings and
   the test method, and open the screen it covers. Then `open_session`,
   which opens the app, and go to that screen. Actions that only got you
   there, such as a sign-in the test's setup replaces, stay out of the test
   when its opening reaches that screen directly; otherwise they are its
   first steps. Node refs are valid only for the newest observation, so
   `observe` again before using new ones. By target:
   - Web: an `E2ETest` class (topic `writing-tests`) whose test opens the
     screen with `App.OpenAsync("/settings/members")`. After `open_session`,
     `call {tool: "navigate", args: {url: "<the path App.OpenAsync names>"}}`.
   - iOS or Android: a `MobileAppTest` class (topic `mobile`) whose test opens
     the screen with `Device.OpenLinkAsync`, or starts on the launch screen
     when the app has no link for it or attaches in the DevFlow preview,
     where `OpenLinkAsync` fails with `UNSUPPORTED_CAPABILITY`. After `open_session`, take the shortest
     path from the launch screen; the actions that took it are the test's
     first steps unless a link reaches the screen.
   - Windows: a `WinAppTest` class (topic `windows`). A desktop app has no
     path: `App.OpenAsync()` relaunches it, and `WinAppTest` already does
     that before each test when the target names `app.appPath`; a target with
     only `app.bundleId` attaches to the running app, so the test starts
     where the walk left it. After `open_session`, take the shortest path from
     the launch window; those actions are the test's first steps.
2. Walk the flow one screen at a time, and write each screen's steps into
   the file before you leave it. Do not walk the whole flow first and write
   the test from memory afterwards. Each step is one of two kinds:
   - An `Agent.ActAsync` goal, the default for a path that varies: write the
     goal with the words on screen and its values in `Params`, then walk its
     path in the session. Those actions stay out of the test, and so do the
     test lines they print.
   - A `Screen` line, for an exact value or interaction: a sign-in form in a
     setup test, a field that must receive one specific string, an
     interaction that is the point of the test. Act on the control by node
     ref, then copy the `Test line:` the action printed into the file. Its
     locator matched that control alone on the live screen. When the action
     prints `No test line:` instead, the reason says why; for a control no
     locator found alone, try one with `locate`.

   With several `Screen` steps on one screen, take their actions, then add
   the lines they printed in one edit.
3. When the outcome shows, add its check before the next step: an
   `Expect.That` on a locator you tried with
   `call {tool: "locate", args: {...}}`, or an `Agent.AssertAsync`.
   One match: use the printed `Screen.GetByRole(...)` call. Zero or several:
   adjust before writing it; the same failure would hit the test as
   `LOCATOR_NOT_FOUND` or `STRICT_MODE`. A role query's `name` matches
   the accessible name only, while `observe` shows a `status`, `alert`, or
   `listitem` by its text: `status "Saved."` on an unlabelled status is its
   text, so match it with `Screen.GetByRole("status").Filter("Saved.")`
   or `Screen.GetByText`.

   When the walk ends, the test is written and every locator you copied
   from a `Test line:` or a `locate` result has matched once; the first run
   checks the rest, such as a `.Filter()` you wrote yourself. If you missed
   a line, `test_lines` returns every line printed so far, in order, the act
   goals' paths included. One walk, in order:

   ```text
   write   MembersTests.cs: usings, the test method, App.OpenAsync("/settings/members")
   call    open_session, then navigate {url: "/settings/members"}   its line is the App.OpenAsync already written
   edit    + await Agent.ActAsync("invite {email} as an editor", new ActOptions { Params = new Dictionary<string, object?> { ["email"] = "ada@example.test" } });
   call    tap, type, select, tap                   the goal's path; its test lines stay out
   call    locate {role: "status"}                  1 node matches, text "Invite sent"
   edit    + await Expect.That(Screen.GetByRole("status")).ToContainTextAsync("Invite sent");
   call    type {target: "n40", text: "ada"}       prints the test line below
           Test line: await Screen.GetByRole("searchbox", "Search members").FillAsync("ada");
   edit    + that line, as printed
   call    locate {role: "listitem"}                1 node matches
   edit    + await Expect.That(Screen.GetByRole("listitem")).ToHaveCountAsync(1);
   ```
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
  the file you write as you walk.
- On the web, a session can stay open while `dotnet test` runs, as long as
  both reach the app. `app.command` starts the app once for the process that needs it, so a
  session and a `dotnet test` run in another process each start their own
  (point both at a fixed port with `reuseExisting`, or start the app
  yourself). On mobile,
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
