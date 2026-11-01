# Running sessions from your own runner

## Contents

- When to use a host
- Open a host and a session
- The options
- Closing: the failures it returns
- Steps and the report
- Limits

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
| `Target` | The target to open. It may be left out only when the config declares one (`TARGET_REQUIRED`; an unknown name is `UNKNOWN_TARGET`) |
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
`OperationCanceledException`. Any other opening failure is a fresh exception with
only its code, category, and message, redacted of the config's secrets.

## Closing: the failures it returns

`session.CloseAsync()` ends the session once and returns
`IReadOnlyList<Exception>`: the soft assertion failures (one `ASSERTION_FAILED`),
the steps the test left running (`STEP_NOT_AWAITED`, and their work is
cancelled), and the cleanup failures. A second call returns the same list. Check
`E2EException.Code` for the code. `DisposeAsync` closes and then throws the
failures as an `AggregateException`, always, even for one. After a session closed
with failures the host refuses another (`WORKER_PROTOCOL`): what it left may be
unclean. `host.CloseAsync()` and `DisposeAsync` do the same for the host.

## Steps and the report

`session.Steps()` returns the timeline as `E2E.Reports.ReportStep`, the typed
form of what `report.json` holds for each step. `Report()` returns the whole
attempt once the session is closed. `TracePath` is where the trace page is
written when the trace mode keeps this attempt (`"trace": "on"` always keeps it).

## Limits

- The engine starts and is disposed once for each session, not once for the host.
- `TARGET_REQUIRED` is not reachable through a config yet: the config declares one target.
- There is no `extend`, so `Fixtures` has no extra members.
