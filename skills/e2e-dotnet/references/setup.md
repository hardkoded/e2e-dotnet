# Setting up e2e for .NET

## Contents

- Requirements
- Scaffold
- Subscriptions and API keys
- The config
- The app under test
- Environment variables
- Mobile targets
- Windows targets
- Done when

## Requirements

- The .NET 10 SDK. The packages target `net10.0`.
- NUnit or xUnit v3. xUnit v2 is not supported.
- Chromium. The first `WebEngine` launch in a test run installs the build it
  launches (the headless shell for a headless run), so the first run needs
  network access. Firefox and WebKit are not exposed yet.
- On a CI image that already has the browser, or with no network, set
  `E2E_SKIP_BROWSER_INSTALL=1` and install it yourself with the `e2e` tool:
  `e2e install` (below), or from the build output:
  `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`.

## Scaffold

There is no `e2e init`. Create a test project and add the packages:

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
engine itself drives Chromium only.

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
| Other | `openai-responses`, `azure`, `google`, `bedrock`, `xai`, `openrouter`, `gateway`, `openai-compatible` | The provider's key; see the README |

- Get model ids from `e2e models <login>`. `e2e models` with no login also lists
  OpenCode Console when only `OPENCODE_API_KEY` is set. Copilot uses a dot
  (`claude-sonnet-5.5`). The Anthropic API uses a dash (`claude-sonnet-5-5`).
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
| `targets` | a `web` target with no URL | One entry: `name`, `platform` (`web`, `ios`, `android`, or `windows`), and `app.url` (a web target) or `app.appPath` and `app.bundleId` (a device or Windows target). A URL without a scheme gets `https://` (`http://` for localhost); plain HTTP is allowed on loopback hosts only, and userinfo, a query, or a fragment is `INVALID_APP_URL`. Without a URL, `App.OpenAsync` takes absolute URLs only. |
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
or `artifacts` keys; each is `INVALID_CONFIG`. Accounts
come from `E2E_USER_<NAME>_USERNAME` and `E2E_USER_<NAME>_PASSWORD`, read
with `Credentials.User(name)`.

A fixture overrides most values with a virtual property of the same name,
such as `BaseUrl`, `CacheMode`, or `ActionTimeout`; named agents come from
`CreateAgents()`. Override `Config` to replace the whole config.

## The app under test

The target declares the app's `url`. `App.OpenAsync(path)` resolves a path
against it. There is no `app.command`: start the app before `dotnet test`,
and point `url` at it.

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
  video, and not `Downloads` yet (no `waitForDownload`). A test fixture needs no runner: an engine that was never
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
  does not call them yet (no `waitForDownload`). The calls were not checked against a live Kernel account.

- To run on the TestMu AI web grid, add the `E2E.TestMu` package and set
  `Browser = new TestMuBrowsers()` (`using E2E.TestMu;`) with `Viewport = null`.
  Set `LT_USERNAME` and `LT_ACCESS_KEY` (or `TESTMU_USERNAME` and
  `TESTMU_ACCESS_KEY`) in the environment that starts the tests. A blank value
  counts as unset, and the lease fails naming each variable that is missing.
  Each session is named `e2e-<run id>-<target>-<slot>`, slots counting from 1.
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

## Mobile targets

iOS and Android are driven by `MobileEngine` in the `E2E.Mobile` project. Install `E2E.Mobile.NUnit` or `E2E.Mobile.XUnit.V3`, declare the app as `"targets": [{ "platform": "ios", "app": { "bundleId": "...", "appPath": "..." } }]`, and derive from its `MobileAppTest`. It needs Node 22.12 or newer, and a booted simulator or emulator. For device selection, hosted Limrun devices, and known limits, read [mobile.md](mobile.md).

## Windows targets

Windows desktop apps are driven by `WinAppEngine` in the `E2E.WinApp` project, through Windows UI Automation. Install `E2E.WinApp.NUnit` or `E2E.WinApp.XUnit.V3`, declare the app as `"targets": [{ "platform": "windows", "app": { "appPath": "bin/MyApp.exe", "bundleId": "MyApp" } }]` (`appPath` is the executable to launch, `bundleId` its process name), and derive from its `WinAppTest`. It runs on Windows only and needs an unlocked desktop for keys and clicks. The UI Automation library ships Windows assets only, so a test project on plain `net10.0` adds `<AssetTargetFallback>$(AssetTargetFallback);net10.0-windows7.0</AssetTargetFallback>` and `NU1701` to `NoWarn`. For the `Window` fixture, the agent tools, and known limits, read [windows.md](windows.md).

## Done when

- `dotnet test` passes against the app.
- `.gitignore` lists `.e2e/`, or everything under it but `.e2e/cache/` to
  commit replays.
- CI runs the suite on pull requests; see topic `running`.
