# Debugging a failing run

## Contents

- Read the failure
- The trace page
- For scripts: report.json
- Error codes
- Watch it happen
- Tools
- Timings and transcripts
- Model calls
- Flaky tests
- Is it the app?

## Read the failure

1. Open the trace page of the failed attempt, `.e2e/results/<result>/trace.md`
   ("The trace page"). `<result>` is the test class, the first words of the
   title, and a hash of both, such as
   `carttests-shop-carttests-checks-out-1a2b3c4d5e6f7a8b`, so titles that read
   alike, or the same title in two classes, keep their own directory. A session
   you start yourself with `E2ESession.StartAsync` names the class with
   `E2ESessionOptions.TestFile`; without it, the title alone sets the name.
   The page names the error and the line to look at, and lists
   every step. The test runner prints the exception too. An `E2EException` has a `Code`;
   read it first, then the message. `AgentException.Blocked` is true when
   credentials, the environment, test setup, or the step's budget stopped
   the step rather than the app.
2. A failed locator expectation says what it expected and the last value it
   read, with the match count. Compare that value with the expectation
   before you change either. The exception's `Details` holds the same facts
   one per field: `Locator`, `Expected`, `Observed`, `Matches`, and for a locator
   `Role`, `Name`, `TestId`, and `WaitedMs` (how long it waited).
3. A failed agent step carries the model's explanation. A passing act's
   `ActResult.Cache` tells whether the step replayed, and why a replay
   missed.

Fix from that evidence, never by walking the flow again from the start over
MCP: the run already saw every screen up to the failure.

- Fix every line it covers, not only the failing one. `Observed` is what the
  screen showed; a node under `Closest to what the locator asked for` is most
  likely what a failed locator meant; the screen at failure (also
  `screen-at-failure.txt`, "The trace page") holds the names the rest of that
  screen's lines need.
- A test stops at its first failed step (`Expect.Soft` lets it go on), and
  every wrong line after it costs another run. Before rerunning, check the
  lines after it as far as you can: against the screen at failure, the app
  source, or a live session.
- For a screen the evidence and the source leave out, look live (topic
  `mcp`): open or reuse a session, go straight to that screen, then `observe`
  and `locate`. On the web, `navigate` to the path of the URL under
  `Screen at failure` or the screen's route, and tap only to reach a state no
  URL opens. `e2e mcp` also opens a device session, which starts on the
  pinned app launched fresh.
- Then rerun the class (topic `mcp` says when a session must close first).

For more than the page tells, read `.e2e/report.json` ("For scripts") or use the
test runner's own output, such as
`dotnet test --logger "console;verbosity=detailed"` or a `trx` logger.
To see the screen the agent reads, call `await screen.DebugAsync()` in the test,
or `await screen.TreeAsync()` to get the text. To see where a slow test spends its time, set `E2E_DEBUG=1`
("Timings and transcripts"). To see what the page did, record it
("Watch it happen").

## The trace page

`trace` is the variable `E2E_TRACE` for one run, or `trace` at the config root,
on a target, or in a fixture's `Trace` property. The modes are `off`, `on`,
`retain-on-failure` (the default), `on-first-retry`, and `on-all-retries`. An
attempt that the mode keeps writes `.e2e/results/<result>/trace.md` when its
session is released, beside the `attempt-<n>` directories. A retry is its own
attempt, and the page tells every attempt the test has had so far: it is
written when any of them kept a trace, so a test that passed on a retry reads
"Passed after 1 failed attempt" with the last failure, and one that failed every
attempt says whether they failed the same way. Open the file; there is
no viewer. It tells, in this order:

1. The title, the test file, the status, and how long the attempt took, then
   `Ran on:` (the browser and its user agent, the device).
2. The error code and message, then the facts of its `Details` (`Expected:`,
   `Observed: ... (1 match)`, `Asked for: button "Save"`, `Waited:`), `Look at:`
   (the test line the failure unwound through, else the failing step's line),
   `Also skipped:` (the reason, when the test called `Skip` and still failed),
   and, after a retry, whether every attempt failed alike.
3. `## Steps`: one line per step, `2. ✗ \`locator.tap\` \`getByRole("button", "Save")\`
   (108ms) — **TIMEOUT** — \`Tests/CheckoutTests.cs:42\``, with the model calls
   of an agent step, the hook the step ran in (`in beforeEach` for a step
   called from an NUnit `[SetUp]`, `in afterEach` from a `[TearDown]`, or from
   an xUnit `InitializeAsync` or `DisposeAsync`), and the offset into the
   video (`, video 0:12`) when one is recorded. A `Browser` or `Device` call is
   a step too, named for the call (`browser.goto` with the URL, `browser.route`
   with the pattern, `device.openLink` with the link before its query,
   `device.setNetwork` with `offline`), so a failure there points at the call,
   and `browser.waitForURL` counts as a verification. Under a step:
   - for a locator action, the node it landed on (`tap button "Save" (12ms)`,
     or `✗` and its code), with `appeared after 340ms` when the node was not
     there at once;
   - for an assertion or a `WaitForAsync`, what it read while it waited
     (`expect gave up after 16 reads in 1.6s: text "0 remaining" (1 match)
     x16`). One value throughout points at a wrong expectation, a broken app,
     or one slower than the timeout. Changing values (`"2 remaining" x3 -> at
     410ms "1 remaining"`) point at a race;
   - what the app logged meanwhile (`✗ network error: POST /api/save 500`,
     `✗ uncaught error: ...`, `⚠ console warning: ...`, `ℹ console: ...`);
   - where the page went (`↪ navigated to /login`, a new tab the app opened,
     a frame that loaded). A navigation the test asked for (`App.OpenAsync`,
     `Browser.GotoAsync`, `ReloadAsync`) is the step itself and is not listed;
   - for an agent step, the cache's decision (`cache: replayed 1 of 3 recorded
     actions, then the agent took over: a recorded target is not on the screen;
     at action 2 of 3, tap button "Apply"`), the model the recording was made
     with when the entry has it (`recorded with gpt-5`), and the entry file;
   - how the screen changed since the step before (`screen: 2 changes since
     step 3` with the added, removed, and changed nodes). `unchanged` after an
     action means it had no visible effect;
   - the explanation of a step that failed.
4. `## App log`, `## Agent turns of step N`, and `## Screen at failure`, when
   the attempt recorded them. `App log` lists up to 50 lines with their step,
   errors and warnings first, and counts the rest. A line from before the
   first step says `before step 1`. An attempt keeps at most 200 errors and
   warnings and 100 other lines, each cut to 300 characters. When the attempt
   fails, the session looks at the app once more before it closes. `Screen at
   failure` gives the URL, the viewport, the node count, a screenshot
   (`attempt-<n>/screenshots/failure.png`, after a secret fill too; none when
   the engine captures no pixels or cannot show that every secure field was
   masked), for a locator that matched nothing or too much the nodes
   closest to what it asked for (`Closest to what the locator asked for`), and
   the screen as the agent reads it, one node per line:
   `#id role "name" text="..." [states]` (also in
   `attempt-<n>/screen-at-failure.txt`). The look is taken the moment the test
   body fails, before the test's own `[TearDown]` or `DisposeAsync`, so one
   that navigates away cannot replace it. A failure that first lands in the
   teardown is looked at after it, as is an xUnit `Expect.Soft` failure, which
   xUnit reports when the test is disposed. When the look fails (a crashed
   page) the section is left out.
5. `## Also failed` (another error, such as one from cleanup) and
   `## Evidence` (the video).

`Agent turns of step N` shows the last 12 turns the model took in the step
that ended the attempt, oldest first: the tool calls (arguments cut to 200
characters) and what came back (cut to 600), with the loop's own notes after
them (`[loop] turn 2 made no tool call: asking for one`). The built-in agent and
any `ToolLoop.CreateToolLoopExecutor` executor record them. An executor of
your own records them with `AttachTurns`.

Not recorded yet, so the page leaves it out: what became of a step's
recording. The app log is what the web engine collects:
console output, uncaught exceptions, failed requests, and 4xx and 5xx
responses. The mobile and document engines report none. A mention, a URL, or markup from the app is shown
as code or escaped, and a secret is redacted. Add `.e2e/results/` to
`.gitignore`.

## For scripts: report.json

Every test process that starts a session writes `report.json` to the output
directory, `.e2e` by default (the parent of the results directory). It holds
every result of the process in a versioned schema, `schema/report-v1.schema.json`
(packed in the `E2E` package, and the same schema upstream ships). The file is
written again, atomically, after each attempt, so `run.finishedAt` and
`run.status` are those of the attempts so far. Two test processes that share an
output directory (the parent of the results directory) overwrite one another's file.

```bash
jq '.run | {status, exitCode, errors}' .e2e/report.json
jq '.run.results[] | select(.status != "passed") | .attempts[-1]
    | {status, error, failure, steps: [.steps[] | select(.status != "passed") | {api, label, source, status, error}], artifacts}' .e2e/report.json
```

`run.exitCode` is the worst exit code of the results: 1 for a failed test,
or the category of an attempt error when that is worse (2 `configuration`, 3
`infrastructure`, 4 `internal`), and 130 for an interruption. `run.status` is
`passed` for 0, `failed` for 1, `interrupted` for 130, and `error` otherwise.
A run is `blocked` instead when every failing result carries a blockable
code. Its exit code is still the worst one: 2 for `AUTH_CREDENTIAL_UNAVAILABLE`,
1 for `STEP_BUDGET_EXHAUSTED`. Every attempt of a failed result counts, as upstream counts them.

`run.errors` holds run-level failures. `run.summary` counts the selected
results: `passed`, `failed` (timed out included), `interrupted`, `flaky`, and
`skipped` add up to `selected`. `run.vcs` names the commit, the branch, and
whether tracked files differ from the commit, when git or the CI variables say.
The last attempt of each failed result holds:

| Field | Holds |
| --- | --- |
| `error` | The code, message, source line, and assertion details |
| `failure` | The screen and screenshot artifact ids, the `url`, and any locator candidates |
| `steps` | The calls that led to the failure. Each step's `events` hold what it did (`engine`), what it polled (`poll`), and where the app went (`navigation`). Agent steps add `cache` and recent model turns, an `observation` event for every look at the screen (the nodes read and its bytes), an `engine` event for every action (`tap`, `type`, `tool:<name>` for a project tool), and `model` events. `observationTruncated: true` (`ReportStep.ObservationTruncated`) means at least one semantic observation was incomplete. `observationRevision` (`b1`, `b2`, ...) names the last screen the step read, and `metrics.observationBytes` is the largest it read. |
| `appLog` | What the app logged. Each line's `step` is the `index` of its step, absent before the first step. |
| `artifacts` | The captured files, with their size and `sha256` |

A session you start yourself with `E2ESession.StartAsync` adds its attempt to that
file too, and after `DisposeAsync` settles, `session.Report()` returns the attempt
alone as a typed `E2E.Reports.Report`, the same document with the same field names
in C# casing (`report.Run.Results[0].Attempts[0].Error`). It is a record the session shares, so treat it as read-only. Its status
is the session's, not your runner's test-body verdict, and artifact paths are relative
to the results directory, not the attempt's directory. It returns `null` before the
session is disposed and when the trace page cannot be written. Use it, with
`CloseSoftFailures()`, in a runner that owns its reporting.

Not in the port's report: serial groups (`run.serialGroups` is always empty),
`carried`, a step's `visionInput`, `visionDegraded`, and `visionOnly`, and the
model fields upstream measures (`peakTokensPerCall` reads 0). A judgment step that
failed before it read a screen has `observationRevision` `unrecorded`. See
`COMPATIBILITY.md` for the full list.

## Error codes

| Code | Usual cause | Fix |
| --- | --- | --- |
| `MODEL_PROVIDER_FAILED` with a network error, 408, 409, 429, or 5xx | The provider failed after the transport retries: up to 5 per model call, honoring `retry-after`, within the step's remaining timeout | Check the provider's status and your quota, then run again (exit 3) |
| `MODEL_PROVIDER_FAILED` with "no response within 120 seconds" | The provider sent no answer in 120 s. This failure is not retried | Check the provider's status, then run again (exit 3) |
| `MODEL_PROVIDER_FAILED` with 401, "x-api-key header is required", or "You didn't provide an API key" | No key reached the provider | Set the provider's variable. Remove a stale `apiKeyEnv` |
| `MODEL_PROVIDER_FAILED` with 400 "not scoped to a workspace" | An Anthropic user key (`sk-ant-usr-`) | Use a workspace key, or `provider: copilot` |
| `MODEL_PROVIDER_FAILED` with "The subscription login failed (NOT_LOGGED_IN)" or `LOGIN_REQUIRED` | No login is stored, or it was rejected | `e2e login <provider>` |
| `MODEL_PROVIDER_FAILED` with a 4xx that names the model | The plan or key does not serve that model id | Pick an id from `e2e models <login>` |
| `MODEL_UNAVAILABLE` | No model is set and no replay finished the step, or GitHub Copilot does not serve the model to the account (`model_not_supported`, a plan or settings limit `e2e models` cannot show) | Set `agents.default.model`; for Copilot, pick an id the plan serves |
| `INVALID_CONFIG` | Unknown key or bad value in `e2e.config.json` | Fix the key the message names |
| `INVALID_ARGUMENT` | A step argument failed validation (an unknown agent, a budget above the agent's) | Fix the call |
| `SECRET_UNAVAILABLE` | `Secrets.Get` named a secret the config does not declare | Add it under `secrets` |
| `AUTH_CREDENTIAL_UNAVAILABLE` | `Credentials.User(name)` found no `E2E_USER_<NAME>_USERNAME` or `_PASSWORD` | Set both variables |
| `LOCATOR_NOT_FOUND`, `NOT_FOUND` | Wrong role or name, inexact text, element off screen, page not open | Read the markup for the accessible name; `exact: false` or a `Regex`; `App.OpenAsync` first |
| `STRICT_MODE` | Two matches (hidden duplicate, repeated label) | A name, a container scope, `Filter(...)`, or `First()`, `Nth(i)` |
| `APP_NOT_OPEN` | A `Screen` call before `App.OpenAsync()` | Open the app first |
| `ASSERTION_FAILED` | Wrong expectation, or the state settles later than 5 s; for `AssertAsync`, a false judgment | Compare with the actual text in the message; `timeout` on the matcher; rewrite the statement. Do not loosen it to pass |
| `ASSERTION_INCONCLUSIVE` | The judge could not decide from what the screen shows | Reach the right screen first; make the statement concrete and visible. When the answer is in pixels (a chart, a canvas), pass `Vision = VisionMode.On` |
| `MODEL_OUTPUT_INVALID` | An `ExtractAsync` or `AssertAsync` answer failed after one repair round | Simplify the type or the question; pick a stronger model |
| `MODEL_REFUSED` | The model refused the prompt (finish reason `content-filter`). Exit 1 | Rephrase the prompt or instruction, or switch to a different model |
| `NOT_ACTIONABLE` | The element is covered, disabled, or does not take that input. The message names the last blocker Playwright logged, such as `<div class="toast">…</div> intercepts pointer events` | `Expect` the condition first. Close the overlay the message names |
| `NODE_STALE` | The element left the page, or Playwright found more than one element (a strict mode violation) before any input was sent. The step looks again | Usually nothing. If it repeats, make the locator match one element |
| `OPERATION_TIMEOUT` | A navigation or a page read (observe, `Browser.EvaluateAsync`, `Browser.TitleAsync`) did not finish within `actionTimeout`. A page stuck in a script fails here, not at the test timeout. On a device, an input the `actionTimeout` ran out before was never sent | Look for a script that never ends or a request that never answers. Raise `actionTimeout` only for a slow UI. A timeout stays `OPERATION_TIMEOUT` and is not retried, even when the call log mentions a detached element or a navigation |
| `ACTION_MAY_HAVE_COMMITTED` | An action, scroll, key press, typing, or mouse call timed out, or hit a strict mode violation, after its input may have reached the page. On a device, a tap, key, swipe, or `Back` the device did not answer within `actionTimeout`. It is never repeated | Check the page state before you retry by hand |
| `TIMEOUT` | `locator.WaitForAsync` timed out | Check the state you wait for |
| `STEP_TIMEOUT`, `STEP_BUDGET_EXHAUSTED` | Goal too big or ambiguous, or a slow provider | Split the goal, use on-screen wording, add vocabulary in `context`; raise `maxSteps` or `maxModelCalls`, `StepTimeout`, or `judgmentTimeout` |
| `CONTEXT_OVERFLOW` | The provider refused the model request as too large for its context window (`ActAsync` after one shrink-and-retry; a judgment on the first refusal) | Lower `agents.<name>.maxObservationBytes`, split the goal into smaller steps, or use a model with a larger context window |
| `REPLAY_STALE` | `cache.strict` is on and a committed recording no longer replays, or the cache directory holds the step's recording under another key (the message names the file and what changed since it was recorded: the engine contract, the replay policy, the agent's context) | Re-run once in `read-write` mode without `cache.strict` to re-record, then commit the changed entry. Delete the old file once nothing replays it |
| `AUTOMATION_UNSUPPORTED` | The step needs an action the agent's tools lack (hover, drag) | Do that step with `Screen` actions |
| `ENVIRONMENT_UNAVAILABLE` with "Chromium is not installed" or "could not install Chromium" | The browser install was skipped or failed | Allow network access for the first run, or run `e2e install` and set `E2E_SKIP_BROWSER_INSTALL=1` |
| `INVALID_APP_URL` | `app.url` has userinfo, a query, a fragment, a non-http(s) scheme, or plain HTTP on a non-loopback host | Fix the URL; use `https://` or a loopback host |
| `APP_UNREACHABLE` | The agent found the app down or not loading | Start the app, or fix `targets[].app.url` |
| `POLICY_DENIED` | A URL whose scheme is not `http:` or `https:` (`file:`, `view-source:`, `data:`), from `OpenAsync`, the agent's navigate step, or `SetCookiesAsync`; a read or expectation of a password field's text, value, or attributes; `ToHaveScreenshotAsync` when the engine cannot prove every secure field on screen was masked | http(s) or `about:blank` only; assert the outcome, not the value; for a screenshot, use an engine that reports its masked regions, or leave the unmaskable field off the screen |
| `UNSUPPORTED_CAPABILITY` | A `Browser` member on the document engine, or an action the engine lacks | Use `WebEngine`, or drop the call |
| `UNSUPPORTED_ARTIFACT` | A `video` mode set on a target, or for a test, whose engine cannot record video | Drop it there, or set it at the config root or in `E2E_VIDEO`, which skip such an engine |

A failed fill of a `Secret` never shows the value. The message reads
`[redacted]` in its place, and the Playwright error is dropped.

A replay miss is not a failure. The step runs live and records again.
`ActResult.Cache.Reason` says why it missed: `no-entry` (nothing recorded
yet), `invalid-entry` (an old or broken file, or a store that could not be
read), `target-not-found` (the control's role or name changed),
`target-ambiguous`, `wrong-context` (the app was on another origin, path, or query), or
`end-mismatch` (every action ran, but the recorded route, a control that
should have appeared or gone away, or a recorded state did not come back;
the outcome was already on screen before the replay; or the replay raised
an alert the recording never saw). Leftover data from an earlier run, a
banner that comes and goes, or a regression causes it.
`ActResult.Cache.ReplayedActions` and `TotalActions` say how far it got.
`ActResult.Cache.Detail` says where the replay stopped and why (`at action 2 of
3, tap button "Apply": ...`). After an `end-mismatch` it names the nodes that
did not show, did not go away, or the alert never recorded, or says it showed
already.

## Watch it happen

```bash
E2E_HEADLESS=0 dotnet test --filter ...                 # a visible browser
E2E_VIDEO=on dotnet test --filter ...                   # record every attempt
E2E_VIDEO=retain-on-failure dotnet test --filter ...    # record every attempt, keep the failures
```

`video` is `off` by default. Set it with `E2E_VIDEO` for one test run (the port's
stand-in for `--video`; `e2e explore --video` is the flag itself), or with `video` at the config root, on a target, or
in a fixture's `Video` property. The modes are `off`, `on`,
`retain-on-failure`, `on-first-retry`, and `on-all-retries`; the retry modes
need NUnit `[Retry]`, and xUnit v3 has no retry. The web engine records the
page with Playwright's screencast. `MobileEngine` records the device screen to
`video/video.mp4` (`MobileOptions.VideoTouches = false` hides the touch
indicator).

The screencast goes into its attempt's directory,
`.e2e/results/<result>/attempt-<n>/video/`: `video.webm`, and one
`video-part<n>.webm` for each later page the attempt opened (after
`App.RestartAsync`, `App.ClearStateAsync`, or a reconnect). A browser provider
that records, such as Kernel, records in place of the screencast: it writes its
own file (`replay.mp4`) into that directory, or a service that keeps the
recording gives a link and no local file. `retain-on-failure` deletes the recording of an attempt that
passed. `new WebEngineOptions { Screencast = ... }` sets the frame size and the
JPEG quality.

A video masks nothing. It shows whatever the screen showed, secrets included,
so keep `video` off unless you need it, and do not share the files. A mode set
on a target is required: an engine that cannot record it (`DocumentEngine`,
the MAUI DevFlow engine) fails the start with `UNSUPPORTED_ARTIFACT`.

## Tools

| Do | When |
| --- | --- |
| `E2E_HEADLESS=0 dotnet test --filter ...` | Watch the failing test in a visible browser |
| `"cache": { "mode": "off" }` | Rule out a stale replay; one run, not every rerun. A failed run deletes the replay that ran into the failure and keeps the ones a check confirmed, so rerun with the cache on first. If the failure repeats after a replay the trace page shows as kept, and the `off` run passes, add or tighten the check right after that goal so a stale replay fails it and is deleted |
| `"actionTimeout": 5000` | While writing a test: a wrong locator fails in 5 s instead of 30 s (`assertionTimeout` for `Expect`). Keep it longer than the slowest screen takes to load |
| `E2E_VIDEO=retain-on-failure` | Watch the failed attempt |
| `E2E_TRACE=on` | Keep a trace page for a passing attempt too |
| `CI=1 dotnet test` | Reproduce CI-only behavior (`read-only` cache) |
| `Browser.WaitForResponseAsync` | Check what the app's API answered |
| `E2E_DEBUG=1 dotnet test` | Phase timings and a table of agent steps per test, and each agent step's transcript |
| `E2E_AI_TRACE=1 dotnet test`, then `npx unbox-ai runs .e2e/ai-trace-<pid>.json` | What the model saw and called |

## Timings and transcripts

`--debug` is the variable `E2E_DEBUG=1` (or `true`) for a test run (`e2e explore --debug` is the flag itself), for example
`E2E_DEBUG=1 dotnet test --logger "console;verbosity=detailed"`. A fixture can
override the `Debug` property instead. Each test then writes a table of its
phase timings to stderr when it ends, slowest phase first:

```console
[e2e debug] phase timings (wall 4.2s)
  phase           count  total    avg    max
  test.body           1   4.0s   4.0s   4.0s
  agent.model         3   2.8s  933ms   1.1s
  session.launch      1  610ms  610ms  610ms
  agent.action        2  240ms  120ms  150ms
  agent.observe       4  180ms   45ms   88ms
  session.close       1   95ms   95ms   95ms
```

The phases are `session.launch` (the engine's start), `test.body` (the session
start until the test settles), `session.close`, and the agent's
`agent.observe`, `agent.model`, and `agent.action`. `test.body` holds the
agent phases, so the rows do not add up to the wall time.

A second table follows, with one row per agent step in execution order: its
total time, the time in its model, observe, and action work, the model calls,
the tokens in and out, the share of the input the provider served from its
prompt cache, and the cost. A `via` column names the model when the steps used
more than one. `cached` and `cost` read `-` when the provider does not report
them. A built-in provider reports the cache split when its response carries it. Only an OpenAI-compatible
endpoint that sends a `cost` in its usage, such as OpenRouter, reports a cost.

```console
[e2e debug] agent steps (execution order, model openai/gpt-5)
  step                                        total  model  observe  action  calls  tokens in/out  cached  cost
  agent.act "increment the counter once"      2.3s   1.9s    180ms    95ms      2        3120/84       -     -
```

Under debug, the transcript a custom executor passes to `AttachTranscript`
is also kept, redacted, as `transcript-<n>.txt` beside the attempt's other
results. Without debug it is dropped.

## Model calls

`--ai-trace` is the variable `E2E_AI_TRACE=1` (or `true`) for a test run (`e2e explore --ai-trace` is the flag itself). A fixture can
override the `AiTrace` property instead. Each test records every model round
trip, and the test process writes them to `ai-trace-<process id>.json` next to
the results directory (`.e2e/` by default) every few seconds and when the process exits. Each test project
runs in its own process, so a run of several projects writes several files.
Delete old ones: a new process does not clear them.

The file has the AI SDK devtools shape, `{ runs[], steps[] }`. One run is one
agent step, named `<test title> · <api> "<label>"`. One step is one model call
of it (an `ActAsync` turn, an `AssertAsync` judgment, a repair round). A step
holds the prompt as sent, the tool schemas, the response, the usage, and any
error. Screenshots are replaced by their byte count, and secrets by their
markers. A step that replays from the cache makes no model call, so it leaves
nothing. Pair it with `"cache": { "mode": "off" }` to capture every call.

```console
npx unbox-ai .e2e/ai-trace-1234.json        # viewer: treemap, waterfall, diffed turns
npx unbox-ai runs .e2e/ai-trace-1234.json   # one line per agent step
```

Do not open the raw file in an agent's context. It repeats the prompt on every
turn and can be several megabytes. Add `.e2e/ai-trace-*.json` to `.gitignore`.

## Flaky tests

- A read (`TextContentAsync`, `CountAsync`) caught a value mid-update: use a
  matcher.
- Shared data: unique names per run with `Values.Unique(...)`, and cleanup
  in `[TearDown]`.
- App not ready: assert on the element you are about to use, not on the
  previous page.
- An agent judgment asserts exact phrasing: judge the fact, and add an
  `Expect.That` beside it.
- Load timing: `[Retry]` masks it; a visible browser shows it.

## Is it the app?

A deterministic step that fails every run at the same place with the same
code is a product bug or a changed screen, not a flake. Reproduce it once
in a visible browser, then fix the app, or update the locator and the
expectation together. A blocked agent step (`AUTH_CREDENTIAL_UNAVAILABLE`,
`ENVIRONMENT_UNAVAILABLE`, `SEED_DATA_MISSING`) points at the environment:
fix that.
