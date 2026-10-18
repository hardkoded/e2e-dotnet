# Debugging a failing run

## Contents

- Read the failure
- Error codes
- Tools
- Timings and transcripts
- Model calls
- Flaky tests
- Is it the app?

## Read the failure

1. The test runner prints the exception. An `E2EException` has a `Code`;
   read it first, then the message. `AgentException.Blocked` is true when
   credentials, the environment, test setup, or the step's budget stopped
   the step rather than the app.
2. A failed locator expectation says what it expected and the last value it
   read, with the match count. Compare that value with the expectation
   before you change either.
3. A failed agent step carries the model's explanation. A passing act's
   `ActResult.Cache` tells whether the step replayed, and why a replay
   missed.

There is no trace page, `report.json`, or `--debug` step table in
this port. Use the test runner's own output, such as
`dotnet test --logger "console;verbosity=detailed"` or a `trx` logger.
To see where a slow test spends its time, set `E2E_DEBUG=1`
("Timings and transcripts").

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

## Tools

| Do | When |
| --- | --- |
| `E2E_HEADLESS=0 dotnet test --filter ...` | Watch the failing test in a visible browser |
| `"cache": { "mode": "off" }` | Rule out a stale replay |
| `CI=1 dotnet test` | Reproduce CI-only behavior (`read-only` cache) |
| `Browser.WaitForResponseAsync` | Check what the app's API answered |
| `E2E_DEBUG=1 dotnet test` | Phase timings per test, and each agent step's transcript |
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
agent phases, so the rows do not add up to the wall time. The port has no
`--debug` agent step table (one row per step with tokens and cost) yet.

Under debug, the transcript a custom executor passes to `AttachTranscript`
is also kept, redacted, as `transcript-<n>.txt` beside the attempt's other
results. Without debug it is dropped.

## Model calls

`--ai-trace` is the variable `E2E_AI_TRACE=1` (or `true`). A fixture can
override the `AiTrace` property instead. Each test records every model round
trip, and the test process writes them to `ai-trace-<process id>.json` next to
the results directory (`.e2e/` by default) when a test ends. Each test project
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
