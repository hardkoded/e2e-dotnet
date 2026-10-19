# Debugging a failing run

## Contents

- Read the failure
- The trace page
- Error codes
- Watch it happen
- Tools
- Timings and transcripts
- Model calls
- Flaky tests
- Is it the app?

## Read the failure

1. Open the trace page of the failed attempt, `.e2e/results/<test>/trace.md`
   ("The trace page"). It names the error and the line to look at, and lists
   every step. The test runner prints the exception too. An `E2EException` has a `Code`;
   read it first, then the message. `AgentException.Blocked` is true when
   credentials, the environment, test setup, or the step's budget stopped
   the step rather than the app.
2. A failed locator expectation says what it expected and the last value it
   read, with the match count. Compare that value with the expectation
   before you change either.
3. A failed agent step carries the model's explanation. A passing act's
   `ActResult.Cache` tells whether the step replayed, and why a replay
   missed.

There is no `report.json` in this port. For more than the page tells, use the
test runner's own output, such as
`dotnet test --logger "console;verbosity=detailed"` or a `trx` logger.
To see where a slow test spends its time, set `E2E_DEBUG=1`
("Timings and transcripts"). To see what the page did, record it
("Watch it happen").

## The trace page

`trace` is the variable `E2E_TRACE` for one run, or `trace` at the config root,
on a target, or in a fixture's `Trace` property. The modes are `off`, `on`,
`retain-on-failure` (the default), `on-first-retry`, and `on-all-retries`. An
attempt that the mode keeps writes `.e2e/results/<test>/trace.md` when its
session is released, beside the `attempt-<n>` directories. A retry is its own
attempt and overwrites the page of the one before it. Open the file; there is
no viewer. It tells, in this order:

1. The title, the test file, the status, and how long the attempt took, then
   `Ran on:` (the browser and its user agent, the device).
2. The error code and message, `Look at:` (the test line the failure unwound
   through, else the failing step's line), and, after a retry, whether every
   attempt failed alike.
3. `## Steps`: one line per step, `2. ✗ \`locator.tap\` \`getByRole("button", "Save")\`
   (108ms) — **TIMEOUT** — \`Tests/CheckoutTests.cs:42\``, with the model calls
   of an agent step, and the offset into the video (`, video 0:12`) when one
   is recorded. Under a step:
   - for an agent step, the cache's decision (`cache: replayed 1 of 3 recorded
     actions, then the agent took over: a recorded target is not on the screen;
     at action 2 of 3, tap button "Apply"`) and the entry file;
   - how the screen changed since the step before (`screen: 2 changes since
     step 3` with the added, removed, and changed nodes). `unchanged` after an
     action means it had no visible effect;
   - the explanation of a step that failed.
4. `## App log`, `## Agent turns of step N`, and `## Screen at failure`, when
   the port recorded them. `Screen at failure` gives the last URL, the node
   count, the viewport, and the last screen the test saw, one node per line
   (also in `attempt-<n>/screen-at-failure.txt`).
5. `## Also failed` (another error, such as one from cleanup) and
   `## Evidence` (the video).

Not recorded yet, so the page leaves them out: what each action landed on, what
an assertion read while it waited, where the app went, what the app logged, the
agent's turns, the screenshot at failure, and what
became of a step's recording. A mention, a URL, or markup from the app is shown
as code or escaped, and a secret is redacted. Add `.e2e/results/` to
`.gitignore`.

## Error codes

| Code | Usual cause | Fix |
| --- | --- | --- |
| `MODEL_PROVIDER_FAILED` with 401, "x-api-key header is required", or "You didn't provide an API key" | No key reached the provider | Set the provider's variable. Remove a stale `apiKeyEnv` |
| `MODEL_PROVIDER_FAILED` with 400 "not scoped to a workspace" | An Anthropic user key (`sk-ant-usr-`) | Use a workspace key, or `provider: copilot` |
| `MODEL_PROVIDER_FAILED` with "The subscription login failed (NOT_LOGGED_IN)" or `LOGIN_REQUIRED` | No login is stored, or it was rejected | `e2e login <provider>` |
| `MODEL_PROVIDER_FAILED` with a 4xx that names the model | The plan or key does not serve that model id | Pick an id from `e2e models <login>` |
| `MODEL_UNAVAILABLE` | No model is set and no replay finished the step | Set `agents.default.model` |
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
| `NOT_ACTIONABLE` | The element is covered, disabled, or does not take that input. The message names the last blocker Playwright logged, such as `<div class="toast">…</div> intercepts pointer events` | `Expect` the condition first. Close the overlay the message names |
| `NODE_STALE` | The element left the page, or Playwright found more than one element (a strict mode violation) before any input was sent. The step looks again | Usually nothing. If it repeats, make the locator match one element |
| `OPERATION_TIMEOUT` | A navigation or a page read (observe, `Browser.EvaluateAsync`, `Browser.TitleAsync`) did not finish within `actionTimeout`. A page stuck in a script fails here, not at the test timeout | Look for a script that never ends or a request that never answers. Raise `actionTimeout` only for a slow UI |
| `ACTION_MAY_HAVE_COMMITTED` | An action, scroll, key press, typing, or mouse call timed out, or hit a strict mode violation, after its input may have reached the page. It is never repeated | Check the page state before you retry by hand |
| `TIMEOUT` | `locator.WaitForAsync` timed out | Check the state you wait for |
| `STEP_TIMEOUT`, `STEP_BUDGET_EXHAUSTED` | Goal too big or ambiguous, or a slow provider | Split the goal, use on-screen wording, add vocabulary in `context`; raise `maxSteps` or `maxModelCalls`, `StepTimeout`, or `judgmentTimeout` |
| `CONTEXT_OVERFLOW` | The provider refused the model request as too large for its context window (`ActAsync` after one shrink-and-retry; a judgment on the first refusal) | Split the goal into smaller steps, or use a model with a larger context window |
| `REPLAY_STALE` | `cache.strict` is on and a committed recording no longer replays, or the cache directory holds the step's recording under another key (the message names the file) | Re-run once in `read-write` mode without `cache.strict` to re-record, then commit the changed entry. Delete the old file once nothing replays it |
| `AUTOMATION_UNSUPPORTED` | The step needs an action the agent's tools lack (hover, drag) | Do that step with `Screen` actions |
| `ENVIRONMENT_UNAVAILABLE` with "Chromium is not installed" or "could not install Chromium" | The browser install was skipped or failed | Allow network access for the first run, or run `e2e install` and set `E2E_SKIP_BROWSER_INSTALL=1` |
| `INVALID_APP_URL` | `app.url` has userinfo, a query, a fragment, a non-http(s) scheme, or plain HTTP on a non-loopback host | Fix the URL; use `https://` or a loopback host |
| `APP_UNREACHABLE` | The agent found the app down or not loading | Start the app, or fix `targets[].app.url` |
| `POLICY_DENIED` | A URL whose scheme is not `http:` or `https:` (`file:`, `view-source:`, `data:`), from `OpenAsync`, the agent's navigate step, or `SetCookiesAsync`; a read or expectation of a password field's text, value, or attributes | http(s) or `about:blank` only; assert the outcome, not the value |
| `UNSUPPORTED_CAPABILITY` | A `Browser` member on the document engine, or an action the engine lacks | Use `WebEngine`, or drop the call |
| `UNSUPPORTED_ARTIFACT` | A `video` mode set on a target, or for a test, whose engine cannot record video | Drop it there, or set it at the config root or in `E2E_VIDEO`, which skip such an engine |

A failed fill of a `Secret` never shows the value. The message reads
`[redacted]` in its place, and the Playwright error is dropped.

A replay miss is not a failure. The step runs live and records again.
`ActResult.Cache.Reason` says why it missed: `no-entry` (nothing recorded
yet), `invalid-entry` (an old or broken file, or a store that could not be
read), `target-not-found` (the control's role or name changed),
`target-ambiguous`, `wrong-context` (the page or path changed), or
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

`video` is `off` by default. Set it with `E2E_VIDEO` for one run (the port's
stand-in for `--video`), or with `video` at the config root, on a target, or
in a fixture's `Video` property. The modes are `off`, `on`,
`retain-on-failure`, `on-first-retry`, and `on-all-retries`; the retry modes
need NUnit `[Retry]`, and xUnit v3 has no retry. The web engine records the
page with Playwright's screencast.

The screencast goes into its attempt's directory,
`.e2e/results/<test>/attempt-<n>/video/`: `video.webm`, and one
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
`MobileEngine`) fails the start with `UNSUPPORTED_ARTIFACT`.

## Tools

| Do | When |
| --- | --- |
| `E2E_HEADLESS=0 dotnet test --filter ...` | Watch the failing test in a visible browser |
| `"cache": { "mode": "off" }` | Rule out a stale replay |
| `E2E_VIDEO=retain-on-failure` | Watch the failed attempt |
| `E2E_TRACE=on` | Keep a trace page for a passing attempt too |
| `CI=1 dotnet test` | Reproduce CI-only behavior (`read-only` cache) |
| `Browser.WaitForResponseAsync` | Check what the app's API answered |
| `E2E_DEBUG=1 dotnet test` | Phase timings and a table of agent steps per test, and each agent step's transcript |
| `E2E_AI_TRACE=1 dotnet test`, then `npx unbox-ai runs .e2e/ai-trace-<pid>.json` | What the model saw and called |

## Timings and transcripts

`--debug` is the variable `E2E_DEBUG=1` (or `true`), for example
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
them.

```console
[e2e debug] agent steps (execution order, model openai/gpt-5)
  step                                        total  model  observe  action  calls  tokens in/out  cached  cost
  agent.act "increment the counter once"      2.3s   1.9s    180ms    95ms      2        3120/84       -     -
```

Under debug, the transcript a custom executor passes to `AttachTranscript`
is also kept, redacted, as `transcript-<n>.txt` beside the attempt's other
results. Without debug it is dropped.

## Model calls

`--ai-trace` is the variable `E2E_AI_TRACE=1` (or `true`). A fixture can
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
