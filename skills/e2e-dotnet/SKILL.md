---
name: e2e-dotnet
description: Writes, runs, and debugs end-to-end tests for web apps with the .NET port of e2e (NuGet packages E2E, E2E.NUnit, E2E.XUnit.V3, E2E.Cli), which mixes Agent.ActAsync goals with exact locator assertions in Chromium. Also drives a project's running app live over the e2e MCP server to look at a screen, check a UI change, take a screenshot, and try a locator. Use when adding an E2E test, setting up e2e.config.json or a model provider, checking a change in the running app, or fixing a failing agent step, locator expectation, or replay-cache miss.
---

# e2e for .NET

An `E2ETest` opens the app in Chromium. `Agent.ActAsync` drives one goal with
a model; `Agent.AssertAsync`, `Agent.WaitForAsync`, and `Agent.ExtractAsync`
judge the screen. `Screen`, `App`, `Browser`, and `Expect` make exact
interactions and checks without a model. A verified agent step is recorded
and replayed on the next run with no model call; judgments still run live.

Text downloads are rescanned before reporting with all secrets known by then,
including later fills and JSON Unicode escapes of ASCII secrets.

## Start here: pick the reference for the job

Read the one reference that matches the request before doing anything else.
Each stands on its own; a reference names another topic only for a side
trip. The installed tool prints the same text with `e2e guide <topic>`
(`e2e guide` alone prints this page), and `e2e mcp` serves it as
`e2e://guide/<topic>`. Differences from the TypeScript
[tester-army/e2e](https://github.com/tester-army/e2e) are listed in
`COMPATIBILITY.md` in the e2e-dotnet repo.

| When you are | Topic | Read |
| --- | --- | --- |
| Adding e2e to a project, writing `e2e.config.json`, signing in to a model, picking a model provider, browser options | `setup` | [references/setup.md](references/setup.md) |
| Looking at the running app, checking a UI change, taking a screenshot, or trying a locator before writing it, over `e2e mcp` | `mcp` | [references/mcp.md](references/mcp.md) |
| Writing or fixing a test: fixture members, locators, actions, matchers, sign-in, the `Browser` member (navigation, routes, cookies, native dialogs, downloads, CSS locators, iframes) | `writing-tests` | [references/writing-tests.md](references/writing-tests.md) |
| Writing a test with NUnit: the `E2E.NUnit` base class, `[Test]`, `[Category("RealModel")]`, `dotnet test --filter`, a complete test | `writing-tests-nunit` | [references/writing-tests-nunit.md](references/writing-tests-nunit.md) |
| Writing a test with xUnit v3: the `E2E.XUnit.V3` base class, `[Fact]`, `[Trait("TestCategory", "RealModel")]`, `dotnet test --filter`, a complete test | `writing-tests-xunit` | [references/writing-tests-xunit.md](references/writing-tests-xunit.md) |
| Adding `Agent` steps, picking a model, budgets, the replay cache, custom executors | `agent` | [references/agent.md](references/agent.md) |
| Running tests: `dotnet test` filters, the `e2e` tool, `report.json` for scripts, exit codes, CI, test filters (not wired into `dotnet test`: the `ITestFilter` seam and the `run.changes` and `run.filters` report fields) | `running` | [references/running.md](references/running.md) |
| Handling a failed run: the trace page, `report.json` for scripts, `E2ESession.Report()` for a session you run yourself, error codes and their fixes, a visible browser, replay misses, phase timings, the agent step table, every model call | `debugging` | [references/debugging.md](references/debugging.md) |
| Exploring an app toward a goal without a test file, with `e2e explore` or `Explore.ExploreAsync` | `explore` | [references/explore.md](references/explore.md) |
| Testing an iOS or Android app: `MobileEngine`, the `Device` fixture, picking a simulator, video of the device screen, Limrun hosted devices, a MAUI app through the DevFlow agent, known limits | `mobile` | [references/mobile.md](references/mobile.md) |
| Testing a Windows desktop app: `WinAppEngine`, `WinAppTest`, the `Window` fixture, the `windows` target in `e2e explore` and `e2e mcp`, the interactive-desktop requirement, known limits | `windows` | [references/windows.md](references/windows.md) |
| Asked to bug bash or hunt for bugs: proving each bug with a repro test | `bug-bash` | [references/bug-bash.md](references/bug-bash.md) |

Two sibling skills install with `npx skills add hardkoded/e2e-dotnet --skill <name>`. `create-verification-skill` writes a project-local `verify-<app>` skill. `migrate-to-e2e` moves an existing Playwright .NET, Selenium .NET, Appium, Cypress, Detox, or Maestro suite to e2e one slice at a time, with the old suite running beside the new one and a `MIGRATION.md` progress file. Hosted Limrun devices are covered in topic `mobile`.

Text downloads are redacted only when they are UTF-8 without NUL bytes; other files stay as served and `incomplete`. See `writing-tests` for download handling.

## Quick start

A config with one web target and a model, and a test that drives one goal
and pins the outcome. `app.command` starts the app for you; leave it out to
start the app yourself before `dotnet test`:

```json
{
  "targets": [{
    "platform": "web",
    "app": {
      "url": "http://127.0.0.1:3000",
      "command": { "executable": "pnpm", "args": ["dev"], "log": ".e2e/logs/app.log" }
    }
  }],
  "agents": { "default": { "provider": "copilot", "model": "claude-sonnet-5.5" } }
}
```

The first test (or `e2e explore`, or `e2e mcp` session) that needs the app
spawns `app.command` and polls `app.readyUrl` (default `app.url`) for a 200 to
499 status within `startupTimeout` (default 60 s); later ones find it running,
and it stops when the test process ends. Never ready is `APP_UNREACHABLE`, with
the last lines of `log` quoted. A `url` answering before the spawn is
`APP_ALREADY_RUNNING` unless `reuseExisting` is `true` (CI ignores it). The
process gets only `PATH`, `HOME`, the temp variables, `SystemRoot` and
`COMSPEC`, `command.env`, and the host variables `inheritEnv` names. A `url` on port `0` (`http://127.0.0.1:0`, or
`[::1]:0`) takes a free port, which `{port}` carries into `args`, `env`, and
`readyUrl`; port `0` needs a `command`. `app.identity` and `app.environment`
(`test`, `staging`, `production`) key the replay cache beside the target; a
device target also takes `app.launchArguments` and `app.permissions`. Topic
`setup` has the full rules.

```csharp
using E2E;
using E2E.NUnit;

public sealed class BillingTests : E2ETest
{
    [Test]
    public async Task Member_upgrades_to_Pro()
    {
        await App.OpenAsync("/settings/billing");
        await Agent.ActAsync("upgrade the workspace to the Pro plan");
        await Expect.That(Screen.GetByRole("status")).ToContainTextAsync("Pro");
    }
}
```

## Workflow: writing a test

1. Look at what exists: `e2e.config.json`, a test project that references
   `E2E.NUnit` or `E2E.XUnit.V3`. Nothing there: follow `setup`.
2. Learn the screens before writing a test: routes, labels, roles, button
   text. Semantic locators need the accessible names the app renders, so read
   the components or watch a run with `E2E_HEADLESS=0`. Over a connected `e2e mcp`
   server (topic `mcp`), each action prints the test line that repeats it.
3. Write an `E2ETest` class. Drive the flow with `Agent.ActAsync`, one
   atomic goal per call, and pin each outcome right after with `Expect.That`
   or `Agent.AssertAsync`. A test with three goals makes three calls.
   Start it on the screen it covers, since every rerun pays for the steps
   before: in a browser a sign-in through the app's API in the test's setup
   (cookies set with `Browser.SetCookiesAsync`; a `Secret` cannot go into a
   request body, so that setup reads its own test account) instead of the sign-in form, and
   `App.OpenAsync` on that screen's path; on a device `Device.OpenLinkAsync`;
   and data from the app's API (topic `writing-tests`). Exact values go
   through `Screen`.
4. Run only the test you are working on:
   `dotnet test --filter "FullyQualifiedName~<Class>.<Method>"`, with the
   settings in "Rerunning a test while you fix it" below. Agent steps need a
   model in the config and that provider's authentication. Tests without
   agent steps need no model. A model request is sent again, up to 5 times,
   after a network failure or a 408, 409, 429, or 5xx answer (waiting the
   provider's `retry-after` when it asks for under a minute, otherwise 2 s
   doubled each time), and only then fails with `MODEL_PROVIDER_FAILED`.
   Bedrock signs with the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` variables or, with none set, the AWS credential chain (shared profile, SSO, container and instance roles). A SigV4-signed Bedrock request (AWS credentials, not a Bedrock API key) is signed again for every attempt, so a long wait does
   not end in `RequestExpired`.
5. When it fails, fix it from what the run recorded (next section).

## Workflow: fixing a failed test

The run already saw every screen up to the failure. Fix from what it
recorded; never walk the flow again from the start over MCP just to see a
screen.

1. Read the exception's `Code`, message, and `Details` (what was expected and
   observed, what the locator asked for, how long it waited). Then read the
   failed attempt's trace page, `.e2e/results/<result>/trace.md`: every step
   with what it did, what the app logged, the agent's turns, and
   `## Screen at failure`, the screen one node per line, with the nodes
   closest to what a failed locator asked for (topic `debugging`).
   An agent step's events tell each look at the screen (`observation`: the nodes
   read and its bytes, `Observation.Revision`, `b1`, `b2`, ...) and each action
   (`engine`: `tap`, `type`, `tool:<name>`); `report.json` keeps the last revision
   as `observationRevision` and the largest read as `metrics.observationBytes`.
   A failed attempt that also carried a skip says `Also skipped: <reason>`.
2. Fix every line that evidence covers, not only the one that failed: the
   screen at failure shows the names the rest of that screen's lines need.
   Fix the locator, the expectation, or the app. Never add a sleep.
3. A test stops at its first failed step (`Expect.Soft` lets it go on), and
   every wrong line after it costs another run. Before rerunning, check the
   lines after it as far as you can: against the screen at failure, the app
   source, or a live session.
4. For a screen the evidence and the source leave out, look live (topic
   `mcp`): open or reuse a session (keep it open across runs while you write
   or fix a web test), go straight to that screen, then
   `observe` and `locate`. On the web, `navigate` to the path of the URL
   under `## Screen at failure` or the screen's route, and tap only to reach
   a state no URL opens. `e2e mcp` opens a device session on a local
   simulator or emulator, or on a hosted device with
   `E2E_MOBILE_PROVIDER=limrun` or `devflow`, and launches the pinned app
   fresh before the first observation (`devflow` attaches to the running
   app). It installs no build, so the target needs `app.bundleId`. A hosted
   device bills until the session closes, so close it when done. Only one
   session can drive a device target at a time. A `windows` target launches the
   executable `app.appPath` names (or attaches to the app `app.bundleId`
   names) before the first observation, on Windows only, and needs an
   unlocked desktop for keys and clicks (topic `windows`).
5. Rerun only that test, the way the next section says (topic `mcp` says
   when a session must close first). Repeat with the next failure.

## Rerunning a test while you fix it

Every run of a failing test waits out its failure, and every `Agent.ActAsync`
that runs live spends model calls. Keep each rerun to the one test and the
lines you changed:

- Filter by class and method, not the project or the suite:
  `--filter "FullyQualifiedName~<Class>.<Method>"`. A filter that matches no
  test runs nothing, so check the name after you rename a class or method.
- Set `"actionTimeout": 5000` in the config (or `ActionTimeout` on the
  fixture) while you iterate: a locator that matches nothing fails in 5 s
  instead of the default 30 s. It also bounds each action inside an agent
  step, so keep it longer than the slowest screen takes to load. A call's own
  timeout still wins. An `Expect` already gives up after 5 s by default
  (`assertionTimeout`).
- Keep the replay cache on. A failed run keeps the recording of every goal
  whose check passed, and the next run replays those with no model call. New,
  changed, and failed goals run live, and so does a replay the trace page
  shows as handed off. `AssertAsync`, `WaitForAsync`, and `ExtractAsync`
  always call the model. Leave `cache.mode` at `read-write` rather than
  `off`: `off` sends every goal to the model again. Only a `read-write` cache
  records: the local default, but not with `CI` set and no cache mode in the
  config, nor under `cache.strict`. A stopped run (Ctrl-C, a command your
  tool's timeout kills) saves nothing, so give the command a timeout longer
  than the test.
- Don't rename the test or its class, and don't reword a goal, change its
  params, or change the agent's `context` once a goal passes. Each is part of
  a recording's key, so every goal it touches records again live. Fixing a
  locator or an expectation keeps the recordings.
- Take `[Retry]` off the test while you iterate (NUnit only; xUnit v3 has no
  retry). The first attempt replays; outside `cache.strict`, each retry runs
  every goal live and waits out the failure again.
- Once the test passes, put `actionTimeout` back and run it one time with
  `"cache": { "mode": "off" }`: every goal runs live at the config's
  timeouts, which shows the agent still reaches it without its recordings.
  Then set the mode back.

## Rules

- Text matches are exact by default. A locator that matches two nodes
  fails with `STRICT_MODE`; narrow it, or add `First()` to an `a.Or(b)`
  whose branches both match (topic `writing-tests`).
- Actions wait for readiness and `Expect` retries. Reads such as
  `TextContentAsync` wait for one match until the action timeout, then answer
  the current value; `CountAsync` and `IsVisibleAsync` answer from the current
  screen. Nothing waits for a value to change, so use a matcher when a value
  has to settle.
- A callback the test hands the app that no step awaits, a `Browser.RouteAsync`
  handler or an `OnDialogAsync` handler, fails the next step with its error.
  When no step follows, the test fails with it at its end, within the cleanup
  timeout (`E2ESession.SettleAttemptAsync`, run by the `E2ETest` base classes
  after the body and again after the teardown); a handler still running then
  fails with `CLEANUP_TIMEOUT`. An engine reports such a failure from
  `IEngineSession.SettleAttemptAsync`, once (topic `writing-tests`).
- To reach a node below the fold, `Screen.ScrollUntilVisibleAsync(locator,
  new ScrollUntilVisibleOptions { Direction, Momentum, Timeout })` scrolls the viewport
  (`ScrollDirection.Down`, `Momentum.Slow` by default) until the locator resolves
  visibly. On a locator (`list.ScrollUntilVisibleAsync(row)`) each step swipes that node,
  so a feed or table that is its own scroll container pages. `Timeout` is its own
  default of 30 s, not `actionTimeout`, and bounds the whole scroll, each swipe
  included. `Momentum` is the stride of each step: a longer one must not carry the
  target past the surface unseen, as a virtualized list does. It fails with
  `LOCATOR_NOT_FOUND` when the deadline expires first (a swipe it cut short
  included) and `INVALID_LOCATOR` for a target made by another screen (topic
  `writing-tests`).
- A read or navigation that times out fails with `OPERATION_TIMEOUT` and is not
  retried, even when the call log mentions a detached element or a navigation
  race. On a device, an input (tap, fill, key, swipe, `Back`) the device does not
  answer within `actionTimeout` fails with `ACTION_MAY_HAVE_COMMITTED` and is
  never repeated; one the timeout ran out before is not sent and fails with
  `OPERATION_TIMEOUT`; a long press that holds at least as long as the time left
  is refused with `NOT_ACTIONABLE`. Reading the screen is not cut off.
- Secrets never appear in test code. Declare them under `secrets` in the
  config and read them with `Secrets.Get(name)`; accounts come from
  `Credentials.User(name)`. Hand the `Secret` only to `FillAsync` or
  `ActOptions.Params`.
- Agent instructions: one goal per act, the wording on screen, real values
  in `Params`. A goal is atomic: one outcome that one check confirms. Two
  outcomes worth checking are two goals, so split them into two `ActAsync`
  calls (topic `agent`). Bad: `"add a todo and mark it done"`. Good: `"add a
  todo"`, check, then `"mark the todo as done"`, check. Judge meaning, not
  phrasing. A judgment reads the text snapshot; when the answer is only in
  pixels (a chart, a canvas, an overlay), pass `Vision = VisionMode.On`, or
  `VisionMode.Only` for the screenshot alone (topic `agent`).
- `ActOptions.OnAction` runs after each action of an act, once the screen
  settles, replayed actions included. Use it to observe (log, screenshot),
  never to drive the app. It is awaited and counts toward `Timeout`. A throw
  fails the step with `agent.act onAction failed: ...`, and a check inside it
  never confirms the act (topic `agent`).
- Check each act's outcome. Only a verified act is recorded and replayed
  (topic `agent`).
- Shape the agent for this app: `context` for the vocabulary the screens
  use, `system` for how it works, named agents under `agents`. A screen over
  the agent's `maxObservationBytes` (default 262144) is cut with an
  `[observation truncated ...]` marker, and a request over `maxInputTokens`
  (default 64000) fails before the model call of a built-in judgment; a custom `ToolLoop` sends its own requests and is not checked first (topic `agent`). A different brain
  is an executor: your own tools in a `ToolLoop`, or an `IStepExecutor` (topic
  `agent`). Every built-in prompt of a test opens with the test's title
  (`Test: <name>`), judges included (a custom executor reads `context.Attempt.TitlePath`), so name a test for what it covers, not
  for the verdict you expect (topic `agent`).
- `.e2e/` is output (`report.json`, `results/`, `cache/`); read it, never edit it. Commit `.e2e/cache/` to share replays. The cache key changed in the version that added the `trace-1` key: recordings from an older version no longer match and must be re-recorded once (topic `running`).
