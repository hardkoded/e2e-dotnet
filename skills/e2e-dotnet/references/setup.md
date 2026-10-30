# Setting up e2e for .NET

## Contents

- Requirements
- Scaffold
- Subscriptions and API keys
- The config
- The app under test
- Environment variables
- App identity and environment
- Browser permissions
- Mobile targets
- Windows targets
- Done when

## Requirements

- The .NET 10 SDK. The packages target `net10.0`.
- NUnit or xUnit v3. xUnit v2 is not supported.
- Chromium, or the browser `WebEngineOptions.BrowserName` names (Firefox or
  WebKit). The first `WebEngine` launch in a test run installs the build it
  launches (the headless shell for a headless Chromium run), so the first run
  needs network access.
- On a CI image that already has the browser, or with no network, set
  `E2E_SKIP_BROWSER_INSTALL=1` and install it yourself with the `e2e` tool:
  `e2e install` (below), or from the build output:
  `pwsh bin/Debug/net10.0/playwright.ps1 install chromium` (or `firefox`,
  `webkit`).

## Scaffold

`e2e init` scaffolds the e2e side of a project from the installed `E2E.Cli`
tool; `e2e init my-app` scaffolds into a new directory. It asks the questions
no flag answered (engines, gateway, skill and MCP locations), shows the changes,
and asks to apply them; a coding agent has no terminal, so it passes `--yes`,
which asks nothing. `--yes` writes `e2e.config.json`
(the web engine and the Vercel AI Gateway as provider `gateway`), installs this
skill in `.agents/skills/e2e-dotnet` (`.claude/skills/e2e-dotnet` symlinks to
it), registers the MCP server in `.mcp.json` and `.cursor/mcp.json`, and
updates `.gitignore`. A flag per question overrides one default: `--engine`
(`web` or `mobile`), `--gateway` (`vercel`, `openrouter`, `openai-compatible`,
`chatgpt`, `copilot`, `opencode-console`, `grok`, or `none`), `--endpoint`,
`--skills`, `--mcp` (comma-separated locations, or `none`); `e2e init --help`
lists the values. `--engine`, `--gateway`, and `--endpoint` apply to a new
config only. Init leaves an existing config alone, refreshes the skill and MCP
entries on a re-run, and never writes through a symlink. A subscription
gateway needs its sign-in before the first run (`e2e login <provider>`).

Init does not create the test project or edit a `.csproj`. Create one and add
the packages:

```bash
dotnet new nunit -n MyApp.E2E
dotnet add MyApp.E2E package E2E
dotnet add MyApp.E2E package E2E.NUnit
```

For xUnit v3, see topic `writing-tests-xunit`. Topic `writing-tests-nunit`
covers the NUnit base class.

Install the `e2e` tool for subscription logins and the MCP server:

```bash
dotnet tool install --global E2E.Cli
```

In a clone of the e2e-dotnet repo, run it as
`dotnet run --project src/E2E.Cli -- <command>`.

`e2e install [chromium|firefox|webkit ...] [--with-deps]` downloads browsers
for the Playwright version `E2E` runs, upstream's `e2e-web install`. It
installs Chromium when no browser is named, and `--with-deps` also installs
the system libraries on Linux. It keeps the other browsers in the Playwright
cache, as upstream does, and exits with Playwright's exit code. An unknown
option or browser exits 2. A local tool runs it as `dotnet e2e install`. The
engine launches the browser `BrowserName` names, Chromium by default.

## Subscriptions and API keys

Pick the model in `agents.<name>.provider` and `model`:

| Goal | `provider` | Auth |
| --- | --- | --- |
| Claude on a subscription | `copilot` | `e2e login github-copilot` (reuses `gh auth token`, or your own `--client-id`) |
| OpenAI on a subscription | `chatgpt` | `e2e login openai` (`--device` without a browser) |
| OpenCode Console (Zen and Go) | `opencode-console` | `e2e login opencode-console`, or `OPENCODE_API_KEY`: a Console service account API key or an OpenCode Go subscription API key. `e2e models opencode-console` reads the key when it is set |
| SuperGrok or X Premium+ | `grok` | `e2e login spacexai` |
| Claude on an API key | `anthropic` | `ANTHROPIC_API_KEY`, a workspace key (`sk-ant-api03-...`) |
| OpenAI on an API key | `openai` (the default) | `OPENAI_API_KEY` |
| Other | `openai-responses`, `azure`, `google`, `bedrock`, `xai`, `openrouter`, `gateway`, `openai-compatible` | The provider's key; see the README (`bedrock`: `AWS_BEARER_TOKEN_BEDROCK`, `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, or the AWS credential chain: profile, SSO, container and instance roles) |

- Get model ids from `e2e models <login>`. `e2e models` with no login also lists
  OpenCode Console when only `OPENCODE_API_KEY` is set. Copilot uses a dot
  (`claude-sonnet-5.5`). The Anthropic API uses a dash (`claude-sonnet-5-5`).
- Every model client sends a request again, up to 5 times, after a network
  failure or a 408, 409, 429, or 5xx answer. It waits the provider's
  `retry-after-ms` or `retry-after` when that is under a minute (or under the
  backoff), otherwise 2 s doubled each time, within the step's remaining
  timeout. A request with no answer in 120 s is not sent again. A SigV4-signed `bedrock` (AWS credentials, not an API key)
  request is signed (SigV4) again for every attempt, with a fresh `x-amz-date`,
  so a retry after a long wait is not refused as `RequestExpired`.
- `google` and `bedrock` send a model's earlier reasoning back on the next turn
  (Gemini thought parts with their signature, Bedrock `reasoningContent`
  blocks), as `anthropic`, `openai-responses`, and `openai` already do. On
  `bedrock` with an Anthropic model id, that reasoning is left out of the next
  request after the loop rewrote an earlier message.
- `apiKeyEnv` overrides the provider's variable. Remove it when you change
  `provider`, or the client sends no key and gets a 401.
- `e2e models github-copilot` marks `not enabled` and `no chat or responses`,
  but it cannot see what the plan serves. On Copilot Free, GitHub serves models
  through auto model selection only, so a model called by id is usually
  refused. The step then fails with `MODEL_UNAVAILABLE` naming the model. Pick
  another id or an API provider.
- A Claude Pro or Max plan cannot serve the agent. Use Copilot or an API key.
- Logins live in `~/.config/e2e/oauth.json`, the file upstream's
  `npx e2e login` writes, so a login made with either tool serves both.
- A cancelled model call stops waiting for a token refresh. The refresh keeps
  running for the other calls that share it.
- Subscriptions are for local runs. Use API keys in CI.

## The config

`e2e.config.json` sits in the test project folder. `E2ETest` finds the
nearest one above the test assembly, then above the working directory. An
unknown key fails with `INVALID_CONFIG`.

```json
{
  "targets": [{ "platform": "web", "app": { "url": "http://127.0.0.1:3000" } }],
  "actionTimeout": 15000,
  "agents": {
    "default": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "context": "Facts about the app the model should know, such as which data it must not change."
    }
  },
  "cache": { "dir": ".e2e/cache" },
  "secrets": { "admin-password": null }
}
```

| Key | Default | Notes |
| --- | --- | --- |
| `targets` | a `web` target with no URL | One entry: `name`, `platform` (`web`, `ios`, `android`, or `windows`), and `app.url` (a web target; port `0` takes a free port) with an optional `app.command` and `app.readyUrl` that start the app (below), or `app.appPath` and `app.bundleId` (a device or Windows target). Any target takes `app.identity` and `app.environment` (below); a device or Windows target takes `app.launchArguments`, and a device or web target `app.permissions` (below). A URL without a scheme gets `https://` (`http://` for localhost); plain HTTP is allowed on loopback hosts only, and userinfo, a query, or a fragment is `INVALID_APP_URL`. Without a URL, `App.OpenAsync` takes absolute URLs only. |
| `timeout` | `120000` | Per test, ms. |
| `launchTimeout` | `60000` | Engine start, ms. |
| `actionTimeout` | `30000` | Each locator action and engine operation, ms. |
| `assertionTimeout` | `5000` | `Expect` polling window, ms. |
| `cleanupTimeout` | `30000` | Engine cleanup, ms: the engine's shutdown after each attempt, and the release of a hosted device when `e2e explore` or `e2e mcp` ends its session. |
| `retries` | `0` | Checked, but not used: retries come from NUnit `[Retry]`. |
| `agents` | none | `default` and any other named agent. Options: topic `agent`. |
| `cache` | `read-write` | `{ mode, dir, strict }`. CI demotes the mode to `read-only`. |
| `secrets` | `{}` | Named values the model never sees; `null` requires `E2E_SECRET_<NAME>`. |
| `trace`, `video` | `retain-on-failure`, `off` | Which attempts record: `off`, `on`, `retain-on-failure`, `on-first-retry`, or `on-all-retries`. Also allowed on the target. The web engine records `video` (topic `debugging`, "Watch it happen"); the port writes no trace page yet. The old `all` and `retries` spellings of `trace` are `INVALID_CONFIG`. |

For `trace` and `video`, `E2E_TRACE` and `E2E_VIDEO` win over the target's
key, which wins over the config root's key. A fixture reads the result in
its `Trace` and `Video` properties. A video keeps whatever the screen shows,
secrets included, so keep it `off` unless you need it. A `video` mode on a
target is required of its engine (`UNSUPPORTED_ARTIFACT` when it cannot
record); one at the config root or in `E2E_VIDEO` skips an engine that cannot.

There are no `credentials`, `tests`, `workers`, `reporters`, `output`,
or `artifacts` keys; each is `INVALID_CONFIG`. A host store for artifacts is code (upstream's `artifacts.store`): override `ArtifactStore` on the fixture, or set `E2ESessionOptions.ArtifactStore`, to an `IArtifactStore`. Its `PutAsync(StoredArtifact)` receives each artifact an attempt registers (a download, a video file, a screenshot or log the test attaches) as it is produced, with `Bytes`, `Sha256`, `Path` (report-relative), `Redaction` (`Complete` or `Incomplete`; a store that exports only what the runner vouches for keeps back an `Incomplete` artifact, a video or a download the runner did not rewrite), and `RunId`, `TestId`, `AttemptId`, `StepId`, `StartedAt`, and returns the reference the report records as the artifact's `ref` beside the local path. `PutLinkAsync(StoredArtifactLink)` receives a video a provider keeps, a link with no bytes (a passed attempt's link under `retain-on-failure` is never handed over). A failed call never fails the run; the record just carries no `ref`. Accounts
come from `E2E_USER_<NAME>_USERNAME` and `E2E_USER_<NAME>_PASSWORD`, read
with `Credentials.User(name)`.

A fixture overrides most values with a virtual property of the same name,
such as `BaseUrl`, `CacheMode`, or `ActionTimeout`; named agents come from
`CreateAgents()`. Override `Config` to replace the whole config.

## The app under test

The target declares the app's `url`. `App.OpenAsync(path)` resolves a path
against it. The `app` also takes `command` and `readyUrl`, which start the
app for you (below), and a `url` with port `0`, which takes a free port.

### Let the config start the app

Prefer `app.command` to a hand-started dev server: self-contained locally
and in CI.

```json
{
  "targets": [{
    "platform": "web",
    "app": {
      "url": "http://127.0.0.1:3000",
      "command": {
        "executable": "pnpm",
        "args": ["dev"],
        "env": { "PORT": "3000" },
        "startupTimeout": 120000,
        "log": ".e2e/logs/app.log"
      }
    }
  }]
}
```

- The first test (or `e2e explore`, or `e2e mcp` session) that needs the app
  spawns `command`, polls `readyUrl` (default `url`) for a 200 to 499 status
  within `startupTimeout` (default 60 s), and the later ones find it running
  and share it: a test, `e2e explore`, and an `e2e mcp` session that declare
  the same command in one process get one instance, and it stops
  (`shutdownTimeout`, default 10 s) when the last of them lets go. Never ready
  is `APP_UNREACHABLE`, and the test fails before the session opens
  (`command.log` still gets this boot's output). The port has no runner, so
  the tests of a process hold the app until the run ends. Add the run fixture
  to stop it then, instead of when the test process exits (on Windows, a process
  killed outright takes the app down with it; on Unix, SIGKILL bypasses the
  process-exit cleanup): in NUnit, a
  `[SetUpFixture]` outside any namespace, `public sealed class AppCommands :
  E2ERunFixture;` (`E2E.NUnit`); in xUnit v3,
  `[assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))]`. Two
  processes, such as `dotnet test` and `e2e mcp`, still start one app each:
  give a shared app a fixed port and `reuseExisting`.
- A `command` is `executable`, `args`, `cwd` (from the config's directory),
  `env`, `inheritEnv`, `startupTimeout`, `shutdownTimeout`, `log`, and
  `reuseExisting`; any other key is `INVALID_CONFIG`, naming the nearest.
  `executable` is started directly, never through a shell, and each `args`
  entry is passed as it is: name the server itself, not a wrapper script, and
  on Windows `pnpm.cmd` is not `pnpm`.
- The child gets only `PATH`, `HOME`, the temp-directory variables,
  `SystemRoot` and `COMSPEC`, and `command.env`. Pass the rest through `env`,
  or name host variables in `inheritEnv` (`["DATABASE_URL", "STRIPE_*"]`);
  `env` wins over them. `inheritEnv` takes names and prefixes ending in `*`,
  never a bare `*`. Model keys and `E2E_USER_*` values are never inherited
  unless a name or prefix matches them.
- Output is discarded unless `log` names a file inside the config's directory,
  such as `.e2e/logs/app.log`; without it a server dying on boot is
  invisible. A startup failure quotes the last 20 lines of what the process
  wrote, with `command.env` and `inheritEnv` values redacted.
- A `url` answering before the spawn is `APP_ALREADY_RUNNING`;
  `command.reuseExisting: true` attaches to a running dev server (CI ignores
  it).
- `"url": "http://127.0.0.1:0"` (or `[::1]:0`, never `localhost:0`) takes a
  free port; the command receives it as `{port}` in `args` or `env`
  (`"args": ["dev", "--port", "{port}"]`), which also expands in `readyUrl`.
  A fixture's `BaseUrl` and `App.OpenAsync` use the port the app was started
  on. A port-0 `url` with no `command`, and `reuseExisting` beside one, are
  `INVALID_CONFIG`. A port grabbed between allocation and spawn fails the
  start with `APP_UNREACHABLE`; rerun.
- `readyUrl` is the probe when it differs from `url`; it must be an http(s)
  URL, and a `command` with neither `url` nor `readyUrl` is
  `APP_URL_REQUIRED`.

For an app started elsewhere, point `app.url` at it and start it before
`dotnet test`.

`WebEngine` takes `WebEngineOptions`. Override `CreateEngine()` to set them:

```csharp
protected override IEngine CreateEngine() => new WebEngine(new WebEngineOptions
{
    Locale = "de-DE",
    TimezoneId = "Europe/Berlin",
});
```

| Option | Meaning |
| --- | --- |
| `Headless` | Unset: headless, unless `E2E_HEADLESS` is `0` or `false`. |
| `Viewport` | Default 1280 by 720; `null` follows the browser window. |
| `TestIdAttribute` | What `GetByTestId` reads; default `data-testid`. |
| `Headers` | Sent only to the base URL's host; turns the browser HTTP cache off. |
| `BasicAuth` | Credentials for a `401` challenge. |
| `UserAgent` | The `User-Agent` every attempt sends. |
| `Locale` | The language every attempt runs in, a BCP 47 tag: `navigator.language`, `Intl`, and `Accept-Language`. An `accept-language` entry in `Headers` beside it is `INVALID_CONFIG`. |
| `TimezoneId` | The IANA time zone every attempt runs in. Spell it with its exact case (`Europe/Berlin`), or it is `INVALID_CONFIG`. |
| `ColorScheme`, `ReducedMotion` | The `prefers-color-scheme` (`WebColorScheme.Light`, `Dark`, `NoPreference`) and `prefers-reduced-motion` (`WebReducedMotion.Reduce`, `NoPreference`) every page reports to its `@media` rules and `matchMedia`, from first paint. A persistent context takes them too. Each defaults to the browser's own. |
| `InitScripts` | Scripts every document runs before the page's own (topic `writing-tests`). |
| `Screencast` | `new WebScreencastOptions { Size, Quality }` for the engine's own video: the frame size in whole pixels (default the viewport's) and the JPEG quality, 0 to 100. Which attempts record is `video`, not this. A browser provider that records ignores it (topic `debugging`). |
| `BrowserName` | `BrowserName.Chromium` (default), `Firefox`, or `Webkit`: the browser the engine launches. Two browsers on one app are two targets, each with its own `WebEngine`. Install an extra one with `e2e install firefox --with-deps` or `e2e install webkit --with-deps`. `Connect` and `Browser` attach over CDP, so they need Chromium: `Connect` with another browser is `INVALID_CONFIG`. Upstream's single `browser` option is a name or a provider; the port splits it into `BrowserName` and `Browser`. |
| `Browser` | An `IBrowserProvider` that leases hosted Chromium browsers (over CDP, or Playwright's protocol for a lease with `Protocol = BrowserProtocol.Playwright`) and releases them when their scope ends. Excludes `Connect`. See below. |
| `Connect` | Attach to a remote Chromium over CDP. `CdpEndpoint` and `ReconnectEndpoint` are resolvers (`ct => Task<string>`), not strings. With `ReconnectEndpoint` it rides one persistent default context and reconnects only to the original browser and page. |

- `ReconnectEndpoint` rides one persistent context without `Headers`,
  `BasicAuth`, `UserAgent`, `Locale`, `TimezoneId` (`INVALID_CONFIG`), or
  `App.ClearStateAsync`. Each attempt needs a fresh browser from `CdpEndpoint`;
  one an earlier attempt rode is refused. Recovery never repeats a dispatched
  operation, and exhausting the action timeout is `OPERATION_TIMEOUT`. After a
  reconnect, observe again before acting on an observed node. Without
  `ReconnectEndpoint` each attempt attaches and creates a new context.

- A provider implements `Name`, `AcquireAsync(BrowserRequest, ct)` returning a
  `BrowserLease` (`Id`, `CdpEndpoint`, `ReconnectEndpoint` for a per-attempt
  lease, and `Protocol`: `BrowserProtocol.Cdp` by default, or `Playwright` for a
  Playwright server or grid, which needs `Worker` scope), and `ReleaseAsync(lease, BrowserReleaseContext, ct)`. Read its token
  from `request.Env`, not the process environment. `Scope` is
  `BrowserProviderScope.Worker` (default: a browser per worker slot, leased
  when the run starts) or `Attempt` (a fresh browser per attempt, released with
  the session). `Attempt` rides one persistent context, with the limits of
  `ReconnectEndpoint` above. Optional `SweepAsync` releases the leases a dead
  worker left open. `Downloads` (`Dir`, `ReadAsync`) and `RecordAsync`
  (returning a `ProviderRecording`) are for a service that holds the browser's
  files and screen; the engine calls `RecordAsync` for an attempt that records
  video, and `Browser.WaitForDownloadAsync` calls `Downloads`. A test fixture needs no runner: an engine that was never
  `Init`ed leases for its one attempt and gives the lease back when the session
  ends.

- To run on Kernel's hosted Chromium, add the `E2E.Kernel` package and set
  `Browser = new KernelProvider()` (`using E2E.Kernel;`), with `Viewport = null`
  when Kernel's screen sets the size. Set `KERNEL_API_KEY` in the environment
  that starts the tests. By default each worker slot gets its own browser, created
  when the run starts and deleted when it ends; `new KernelOptions { Scope =
  BrowserProviderScope.Attempt }` leases one per attempt instead, with the limits
  of `ReconnectEndpoint` above. `KernelOptions` takes Kernel's create-browser
  body (`Stealth`, `Headless`, `Gpu`, `TimeoutSeconds`, `Profile`, `ProxyId`,
  `Viewport`, `Tags`, and `Extra` for any other field), plus `Replay` (a Kernel
  replay of the screen for an attempt that records video, the default; `false`
  keeps the screencast) and `ReplayParams` (`Framerate`, `MaxDurationInSeconds`,
  `RecordAudio`). Every browser is tagged `e2e_run`, `e2e_target`, `e2e_slot`, and
  `e2e_attempt`, `TimeoutSeconds` defaults to 600, and a sweep at the end of the
  run deletes the browsers a dead worker left. A replay is
  the attempt's `video/replay.mp4`. Downloads are implemented, but the engine
  reads a download through them with `Browser.WaitForDownloadAsync`. The calls were not checked against a live Kernel account.

- To run on the TestMu AI web grid, add the `E2E.TestMu` package and set
  `Browser = new TestMuBrowsers()` (`using E2E.TestMu;`) with `Viewport = null`.
  Set `LT_USERNAME` and `LT_ACCESS_KEY` (or `TESTMU_USERNAME` and
  `TESTMU_ACCESS_KEY`) in the environment that starts the tests. A blank value
  counts as unset, and the lease fails naming each variable that is missing.
  Each session is named `e2e-<run id>-<target>-<slot>`, slots counting from 1 (a per-attempt lease is `e2e-<run id>-<target>-attempt-<attempt id>`).
  `TestMuBrowsersOptions` sets `BrowserName` (a Chromium browser), `BrowserVersion`,
  `Platform`, `Build`, `Video`, `Network`, `Console`, `TunnelName`,
  `GeoLocation`, and `Capabilities` for any other `LT:Options` entry.
  `Platform` defaults to `Windows 11`, `project` to `e2e`, and `idleTimeout` to 600 seconds. For Edge
  (`BrowserName = "MicrosoftEdge"`), the provider adds
  `--user-data-dir=remote-debug-profile-edge` to `browserOptions` unless a
  `--user-data-dir=` is there. `new TestMuBrowsers(options)` throws
  `ConfigurationException` (`INVALID_CONFIG`) for a `Capabilities` entry named
  `LT:Options`, `browserName`, `user`, `accessKey`, or `name`, a
  `browserOptions` that is not an array of strings, or a number JSON cannot
  encode. The
  engine connects with Playwright's own protocol (`BrowserLease.Protocol` is
  `Playwright`), not CDP, and opens its own browser context. The
  dashboard shows the sessions without a Passed or Failed mark, because e2e
  passes no test result to a provider.

To test without a browser or model, override `CreateEngine()` to return a
`DocumentEngine`, and `CreateModel()` to return a scripted `IAgentModel`.

## Environment variables

| Variable | Effect |
| --- | --- |
| `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, ... | The provider's key, or the variable `apiKeyEnv` names. |
| `E2E_USER_<NAME>_USERNAME`, `E2E_USER_<NAME>_PASSWORD` | The account `Credentials.User(name)` returns; the name is uppercased and `-` becomes `_`. |
| `E2E_SECRET_<NAME>` | Overrides `secrets.<name>`. |
| `E2E_OAUTH_CREDENTIALS` | The login JSON, in place of the credentials file. |
| `OPENCODE_API_KEY` | An OpenCode Console service account API key or an OpenCode Go subscription API key, in place of the `opencode-console` login. `Subscriptions.OpenCodeConsole` and `e2e models opencode-console` use it. |
| `CI` | CI defaults: a `read-only` cache. |
| `E2E_TRACE`, `E2E_VIDEO` | A recording mode (`off`, `on`, `retain-on-failure`, `on-first-retry`, `on-all-retries`) for the run, over the config. Upstream sets these with `--trace` and `--video`. `E2E_VIDEO` records on the web engine. A bad value is `INVALID_CONFIG`. A blank value is ignored. |
| `E2E_HEADLESS` | `0` or `false` shows the browser. |
| `E2E_SKIP_BROWSER_INSTALL` | `1` skips the Chromium install. |

## App identity and environment

`app.identity` is the stable name that keys the replay cache: the recordings of one app are found again by it. Unset, it is the URL's origin and path as declared (a port-`0` URL keeps the `:0`, so the port changing every run never misses the cache), else `app.bundleId`, else `app.appPath`. Declare it when the origin changes per deploy (a PR preview), so the preview shares the entries of the app it deploys; different apps need different identities. `app.environment` is `test`, `staging`, or `production`. Unset, it is `test` for a loopback, `.localhost`, or `.test` host and for a target without a URL, and `production` for any other host. The environment always joins the cache key, so an identity never shares entries across environments. The site of the URL (the last two labels of its host, three for `example.co.uk`) is derived, not declared.

```json
{ "targets": [{ "platform": "web", "app": { "url": "https://pr-12.preview.example.dev", "identity": "shop", "environment": "staging" } }] }
```

Declaring an identity or environment for an app that had none misses the entries recorded before: they are not read, and the next run records them again.

## Browser permissions

Headless Chromium refuses a permission the target does not grant, so `navigator.clipboard.writeText` fails even after a click. A web target's `app.permissions` grants it to every page of every attempt, on any origin, after a restart or `ClearStateAsync` too:

```json
{ "targets": [{ "platform": "web", "app": { "url": "http://localhost:3000", "permissions": { "clipboard-read": "grant", "clipboard-write": "grant" } } }] }
```

A web target takes `grant` only; `deny` and `reset` are `INVALID_CONFIG` (leave the permission out instead). The names are Playwright's, and each browser grants its own set; an unknown one is `INVALID_CONFIG` naming the nearest, checked when the attempt starts. A fixture overrides `Permissions` to change them in code.

| Browser | Names |
| --- | --- |
| `chromium` | `accelerometer`, `ambient-light-sensor`, `background-sync`, `camera`, `clipboard-read`, `clipboard-write`, `geolocation`, `gyroscope`, `local-fonts`, `local-network-access`, `magnetometer`, `microphone`, `midi`, `midi-sysex`, `notifications`, `payment-handler`, `screen-wake-lock`, `storage-access` |
| `firefox` | `geolocation`, `local-network-access`, `notifications`, `persistent-storage`, `push`, `screen-wake-lock` |
| `webkit` | `camera`, `clipboard-read`, `geolocation`, `microphone`, `notifications`, `screen-wake-lock` |

A browser provider or `Connect` takes the `chromium` names: the engine drives it over CDP.

## Mobile targets

iOS and Android are driven by `MobileEngine` in the `E2E.Mobile` project. Install `E2E.Mobile.NUnit` or `E2E.Mobile.XUnit.V3`, declare the app as `"targets": [{ "platform": "ios", "app": { "bundleId": "...", "appPath": "..." } }]`, and derive from its `MobileAppTest`. It needs Node 22.12 or newer, and a booted simulator or emulator that no other agent-device session holds (with none booted, agent-device boots one; with every booted one held, `prepare` fails with `ENGINE_FAILURE`). For device selection, hosted Limrun devices, and known limits, read [mobile.md](mobile.md).

## Windows targets

Windows desktop apps are driven by `WinAppEngine` in the `E2E.WinApp` project, through Windows UI Automation. Install `E2E.WinApp.NUnit` or `E2E.WinApp.XUnit.V3`, declare the app as `"targets": [{ "platform": "windows", "app": { "appPath": "bin/MyApp.exe", "bundleId": "MyApp" } }]` (`appPath` is the executable to launch, `bundleId` its process name), and derive from its `WinAppTest`. It runs on Windows only and needs an unlocked desktop for keys and clicks. The UI Automation library ships Windows assets only, so a test project on plain `net10.0` adds `<AssetTargetFallback>$(AssetTargetFallback);net10.0-windows7.0</AssetTargetFallback>` and `NU1701` to `NoWarn`. For the `Window` fixture, the agent tools, and known limits, read [windows.md](windows.md).

## Done when

- `dotnet test` passes against the app.
- `.gitignore` lists `.e2e/`, or everything under it but `.e2e/cache/` to
  commit replays.
- CI runs the suite on pull requests; see topic `running`.
