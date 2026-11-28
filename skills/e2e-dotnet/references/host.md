# Running sessions from your own runner

## Contents

- Open a host and a session
- The options
- Closing: the failures it returns
- Steps and the report
- Limits
- Launch without a config or a runner

`E2EHost` (upstream's `openHost`) is for a test runner that is not NUnit or
xUnit and wants isolated per-test sessions: it loads the config, starts the
target's app command, prepares the engine, and opens one `E2ESession` at a
time. The NUnit and xUnit v3 base classes do not need it.

## Open a host and a session

```csharp
await using var host = await E2EHost.OpenAsync(new OpenHostOptions { Cwd = projectDir });
await using var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "home screen" });

await session.Fixtures.App.OpenAsync("/");
await Expect.That(session.Fixtures.Screen.GetByRole("button", "Sign in")).ToBeVisibleAsync();
```

`session.Fixtures` is the same `TestContext` as `session.Context`. Open sessions
one after another: a host has one opening, live, or closing session at a time.
A session is `E2ESession`, with `RunId`, `AttemptId`, `Target`, `ArtifactsDir`,
`TracePath`, `Steps()`, `Report()`, and `CloseAsync()`.

## The options

`OpenHostOptions`:

| Member | Holds |
| --- | --- |
| `Cwd` | Where the config file is searched for upward. Default: the current directory |
| `Config` | An `E2EConfig` value (`E2EConfig.Parse(json, projectRoot)`), used instead of a file. Its `ProjectRoot` is the project root |
| `ConfigPath` | A config file, relative to `Cwd`. It is loaded fresh for each host |
| `Target` | The target to open. It may be left out only when the config declares one target (several without it is `TARGET_REQUIRED`; an unknown name is `UNKNOWN_TARGET`) |
| `Env` | The variables the config and the engine read. Default: the process environment |
| `Headed` | A visible window for a `web` target |
| `OnNotice` | `(target, line)` for provisioning, leases, and app processes. It must not throw |
| `EngineFactory` | .NET-only. Builds the engine of the selected target. A config file holds no engine, so a `web` target gets a `WebEngine` and any other platform, or a test engine, comes from here |

`OpenSessionOptions`: `Name` (required; untrusted text, never a raw path),
`Timeout` (counted from open; default the config's `timeout`; zero, negative,
or infinite is `INVALID_CONFIG`), `Agent`, and `OnStep`, which gets each
`E2E.Reports.StepProgress` as the step runs and must not block or throw.

The token you pass to `OpenAsync` cancels provisioning, initialization, and every
session's operations; `CloseAsync` still releases. The token you pass to
`OpenSessionAsync` cancels that opening. A cancel stays an
`OperationCanceledException`. A session whose operation that cancel cut ends as
`interrupted`, not passed: its trace says so, and a `retain-on-failure` video is kept.
Any other opening failure is a fresh exception with
only its code, category, and message, redacted of the config's secrets.

## Closing: the failures it returns

`session.CloseAsync()` ends the session once and returns
`IReadOnlyList<Exception>`: the soft assertion failures (one `ASSERTION_FAILED`),
the steps the test left running (`STEP_NOT_AWAITED`, and their work is
cancelled), and the cleanup failures. A second call returns the same list. Check
`E2EException.Code` for the code. `DisposeAsync` closes and then throws the
failures as an `AggregateException`, always, even for one. After a session whose cleanup failed, or a failed launch whose engine could not end what it opened,
the host refuses another (`WORKER_PROTOCOL`): what it left may be unclean. A failed launch's cleanup failure comes back from `host.CloseAsync()`. A soft
failure or an un-awaited step does not do that. `host.CloseAsync()` and `DisposeAsync` do the same for the host.

A hosted session has no test body to fail, so its steps decide the verdict: with no timeout, soft failure, or un-awaited step, the first step that did not pass (failed, `blocked`, timed out, or cancelled) makes the attempt non-passing. That verdict drives the trace and the `video` mode: under `retain-on-failure` a session whose agent step ended `blocked` keeps its video. The host reads `agents.default`'s executor from a config you build with `E2EConfig.WithExecutor`, as the runner does.

## Steps and the report

`session.Steps()` returns the timeline as `E2E.Reports.ReportStep`, the typed
form of what `report.json` holds for each step. `Report()` returns the whole
attempt once the session is closed. `TracePath` is where the trace page is
written when the session closes. A hosted session always writes it, whatever the
trace mode. The page stands alone: it has no `report.json` to point at.

## Limits

- The engine starts and is disposed once for each session, not once for the host.
- There is no `extend`, so `Fixtures` has no extra members.

## Launch without a config or a runner

For a script, a scraper, or a bot there is no config file and no report: `Launch.LaunchAsync` (upstream's `launch` from `@e2e-dev/core`) takes an engine and
the app, and returns a `Host` that opens one `Session` at a time. A session holds `Screen`, `App`, `Platform`, and `Browser`. An engine that contributes fixtures (`IEngine<TFixtures>`, such as `MobileEngine` with `MobileFixtures`) makes `LaunchAsync` return a `Host<TFixtures>` whose sessions are `Session<TFixtures>`: `session.Fixtures.Device` is the device fixture. `WebEngine` is one too (`WebFixtures`): `session.Fixtures.Browser` is the browser fixture, and `session.Fixtures.App` is a `WebApp`, the `App` with `BaseUrl` always set, since a web target always declares `app.url`.

```csharp
await using var shop = await Launch.LaunchAsync(new WebEngine(), new LaunchOptions
{
    App = new LaunchApp { Url = "https://shop.example.com" },
    Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = password },
});
await using var session = await shop.NewSessionAsync();
await session.App.OpenAsync("/signin");
await session.Screen.GetByLabel("Password").FillAsync(shop.Secret("SHOP_PASSWORD"));
```

- Each host hands its engine a random token as `HostId` (`IEngineHostIdentity`, in prepare, the engine's start, and finish). A `MobileEngine` host drives its device under `e2e-<target>-<token>-<slot>`, a session of its own (the `Session` option still names it whole), and never takes a device another session holds: boot one free device per host, and open device hosts one after another. A host that ends without `CloseAsync`, or whose session fails to close, leaves its device held: `CloseAsync` then throws an error that names the session, and `npx agent-device close --session "SESSION"` frees it. A host's artifact folders are `core/host-<token>-<attempt id>` under `ArtifactsDir`.
- A second `LaunchAsync` on an engine instance another host owns, and a second open session on a host, fail with `WORKER_PROTOCOL`. Parallel work is more hosts.
- Inside a test (a test class that derives from `E2ETest`), a host and the sessions it opens are the test's: their steps are in its report under `launch-1`, `launch-2` in the order the test opened the sessions (`ReportStep.Actor`, `StepProgressIdentity.Actor`), a session's video is kept beside the test's as `video/launch-1.webm` (`ArtifactRecord.Actor`) when the test records one, the list reporter, the trace page and the Markdown report name the session (`launch-1 › locator.tap`, `at step 3 of 5 on launch-1`), and a host or session the test left open is closed with it. A step left running, or a `NewSessionAsync` or `LaunchAsync` call not awaited, fails the test with `STEP_NOT_AWAITED`. A host opened outside every test (at the top of a file, in a fixture) is left open, and its steps are recorded in each test that uses it. The test is found through the async flow that started its session, so a host launched from a thread the test body did not start is no test's.
- A secret value is at least 6 characters, and is redacted from the errors a session throws and from `Screen.TreeAsync()`. `Host.Secret(name)` with another name is `SECRET_UNAVAILABLE`.
- `LaunchOptions.Timeout` bounds an action, a wait, a read, and `App.OpenAsync()` or `App.RestartAsync()` (30 seconds for the first three and 120 seconds for the last two without it). `session.SetDefaultTimeout(timeout)` moves all of them for the operations that start from then on; a zero or infinite span is `INVALID_ARGUMENT`. There is no session deadline. `Headed` shows the browser window (a device ignores it). An operation on a closed session fails with `CANCELLED` and the message `the session is closed`. `ArtifactsDir` keeps screenshots and downloads (a relative path resolves from `Cwd`); without it they go to a temp directory deleted when the host closes.
- `await session.SaveStateAsync()` captures cookies and storage in memory. Pass it to `shop.NewSessionAsync(new NewSessionOptions { State = state })`. To keep it between runs, `state.ExportAsync(new SessionStateKeyOptions { Key = key })` with a 32-byte key you keep, and `SessionState.ImportAsync(bytes, options)`. A state restores only on the same engine, platform, and app (`SESSION_MISMATCH`; give two addresses of one app the same `LaunchApp.Identity`), a wrong key or changed byte is `SESSION_INVALID`, and an old one `SESSION_EXPIRED`.
- `LaunchApp.LinkBase` (an absolute URL or a scheme such as `shopapp://`, with no query or fragment) makes `session.App.OpenAsync("/orders/42")` open `shopapp://orders/42` in the pinned app of a device engine. An absolute link opens as given; `file:`, `data:`, `javascript:`, `view-source:`, `blob:`, and `filesystem:` are `POLICY_DENIED`; an engine without a link hook, or a bad value, is `INVALID_CONFIG` at launch.
- `CoreAgents.CreateAgent(session, new CreateAgentOptions { Model = model, Cache = new AgentCacheOptions { Dir = ".cache" } })` puts an agent over a session: `ActAsync`, `AssertAsync`, `WaitForAsync`, and `ExtractAsync` as in a test. `CreateAgentOptions` is an `AgentOptions` plus `Cache` (`Dir`, `Mode`, `Store`, `Strict`). No model fails the first call with `MODEL_UNAVAILABLE`; `ActAsync` takes no `Agent` option (`INVALID_ARGUMENT`).
- With `Cache`, each `ActAsync` names its recording, `new CachingActOptions { CacheKey = "checkout" }`, or runs live with `Cache = false`; without either, or with a `CacheKey` on an agent without a cache, it is `INVALID_ARGUMENT`. A recording is keyed by the `CacheKey`, the instruction, its params, the app, and how often the session made the same call. It is saved when the session or its host closes, and only if a check passed after it (`Locator.WaitForAsync`, `AssertAsync`, `WaitForAsync`). An agent step that fails drops the recordings not yet confirmed (not when only a missing model explains it); a failed locator call of your own code drops nothing.
- Not ported yet: closing on signals (issue 1677).

A `Host` shares its `Secrets` with every session open beside it until it closes, and a session shares the values it learned while a host is open, so a session redacts a value only another session was given. What a session holds stays its own: a saved state carries the values the session knows, the host's configured values included, and none it only borrows through shared redaction. A cause whose details alone carry a secret value is rebuilt redacted.
