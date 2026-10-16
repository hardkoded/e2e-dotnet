# E2E for .NET

Community [.NET](https://dotnet.microsoft.com) port of [e2e](https://github.com/tester-army/e2e), the agentic end-to-end testing framework. Describe a goal in natural language, let an agent drive the app, then check the result with locators.

This is **not** an official TesterArmy product and is not endorsed by TesterArmy. It reimplements the public testing flow. It does not copy the TypeScript sources. Behavior that is intentionally different is listed in [COMPATIBILITY.md](COMPATIBILITY.md).

License: Apache License 2.0. Copyright 2026 TesterArmy.

Docs and API reference: https://hardkoded.github.io/e2e-dotnet/

## Install

```bash
dotnet add package E2E
dotnet add package E2E.NUnit   # or E2E.XUnit.V3 for xUnit v3
```

The library targets `net10.0` and includes `WebEngine`, which drives Chromium through [Microsoft.Playwright](https://playwright.dev/dotnet/). Firefox and WebKit are not exposed yet. The first `WebEngine` launch in a process installs Chromium, so the first run needs network access. A headless run installs only the headless shell, the build it launches. When that build is already installed, the step does nothing. The install keeps other browsers in the Playwright cache; set `PLAYWRIGHT_SKIP_BROWSER_GC=0` to let it remove them. On a machine that already has the browser, or has no network, set `E2E_SKIP_BROWSER_INSTALL=1` to skip it, and install it yourself from the build output:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

Or, with the `e2e` tool (see [Subscriptions](#subscriptions)), `e2e install [chromium|firefox|webkit ...] [--with-deps]`. It downloads the browsers for the Playwright version `E2E` runs, installs Chromium when none is named, and `--with-deps` adds the system libraries on Linux. An unknown option or browser exits 2.

## A test

```csharp
using E2E;
using E2E.NUnit;
using NUnit.Framework;

public sealed class BillingTests : E2ETest
{
    protected override IAgentModel? CreateModel() =>
        new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "gpt-4.1-mini" });

    [Test]
    public async Task Member_upgrades_to_Pro()
    {
        await App.OpenAsync("/settings/billing");
        await Agent.ActAsync("upgrade the workspace to the Pro plan");
        await Agent.AssertAsync("the invoice preview shows a prorated amount");
        await Expect.That(Screen.GetByRole("status")).ToContainTextAsync("Pro");
    }
}
```

`E2ETest` starts a `WebEngine` session for each `[Test]`. Override `CreateEngine` with a `DocumentEngine` when the test should not open a browser, or with a `WebEngine` whose `Connect` attaches to a remote Chromium over CDP (see [Recovering a CDP transport](#recovering-a-cdp-transport)), or whose `Browser` leases hosted browsers (see [Hosted browsers](#hosted-browsers)). An `act` that a later `assert`, `waitFor`, or locator `Expect` verifies is recorded. The next run replays those actions with no model calls until the screen no longer matches. When the test ends, verified acts are written and unverified acts that recorded or replayed are evicted, whether it passed, failed, or was skipped. `[Retry]` runs the later attempts live, and they still record. Tests that never call the agent need no model. A judgment (`AssertAsync`, `WaitForAsync`, `ExtractAsync`) reads the text snapshot of the screen. Pass `Vision = VisionMode.On` to show it a masked screenshot too, or `VisionMode.Only` for the screenshot alone, when the answer is in pixels. After a test fills a `Secret`, no pixels leave the session.

### Recovering a CDP transport

`WebConnectOptions.ReconnectEndpoint` opts into recovery of the same remote browser after its CDP transport disconnects. This mode uses the browser's persistent default context, because Chrome deletes ordinary Playwright contexts when their transport detaches.

```csharp
new WebEngine(new WebEngineOptions
{
    Connect = new WebConnectOptions
    {
        CdpEndpoint = ct => hosted.ProvisionAsync(ct),
        ReconnectEndpoint = ct => hosted.EndpointAsync(ct),
    },
});
```

In this mode, `CdpEndpoint` provisions a fresh, dedicated browser at every attempt start, including retries. The engine rejects a browser reused by a previous attempt of the same `WebEngine`. The remote must have only its default context, and the host owns the browser: disposing the session closes the connection, not the browser. The attempt's first page is the browser's own first tab, navigated to `about:blank`.

After a disconnect, the next operation calls `ReconnectEndpoint` once, bounded by the action timeout and its cancellation token. This resolver must return the existing browser's endpoint. The engine verifies the default context id and the original page's target id; a URL match is not enough to pick a replacement tab. Recovery also requires the page's frames to keep the closed shadow root hook, which a document that navigated while disconnected lost. A different browser, a missing page, or a resolver that runs out of time fails the attempt, and every later operation fails the same way. Recovery never repeats an operation that was already dispatched. Route handlers and init scripts, configured and added, are reattached, and each document still runs every init script once. After a reconnect, `PerformAsync`, the agent's key press, and the viewport swipe fail with `NODE_STALE` until the screen is observed again; the `Browser` keyboard and mouse keep working.

Persistent recovery does not support `Headers`, `BasicAuth`, `UserAgent`, `Locale`, or `TimezoneId` (`INVALID_CONFIG`), or `App.ClearStateAsync` (`UNSUPPORTED_CAPABILITY`). `App.RestartAsync` remains available. `ColorScheme` and `ReducedMotion` work in this mode: the engine sets them on each page it opens. Omit `ReconnectEndpoint` to keep a new, isolated context per attempt.

### Hosted browsers

`WebEngineOptions.Browser` takes an `IBrowserProvider`: an object that leases a browser from a service and hands back the CDP endpoint to attach to. The engine attaches over CDP the way `Connect` does, and adds the lifecycle `Connect` lacks: it asks for a browser when one is needed and gives it back when its scope ends, on every exit path, so a session billed by the minute stops when the run no longer uses it. The engine knows no vendor; for any service a provider is a few dozen lines in your project. A provider implies Chromium and cannot be combined with `Connect`. Add `Viewport = null` when the service shows the window in a live view, so the page fills the window.

```csharp
public interface IBrowserProvider
{
    string Name { get; }                                       // label in progress lines and error messages
    BrowserProviderScope Scope => BrowserProviderScope.Worker; // Worker: a browser per worker slot. Attempt: a fresh browser per attempt.
    Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken);
    Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken); // optional
    BrowserProviderDownloads? Downloads => null;               // optional: serves downloads of a browser on another machine
    Task<ProviderRecording?> RecordAsync(BrowserLease lease, ProviderRecordContext context, CancellationToken cancellationToken); // optional
}

public class BrowserLease
{
    public string Id { get; init; }                 // the provider's handle (a session id), handed back to ReleaseAsync
    public string CdpEndpoint { get; init; }        // ws://, wss://, or http:// DevTools endpoint to attach to
    public string? ReconnectEndpoint { get; init; } // to reattach to the same browser after a transport drop; per-attempt leases only
}
```

`AcquireAsync` receives the run id, target, worker slot and slot count, the attempt id for a per-attempt lease, the run's environment, and a `Log` that reaches the reporter (an engine started without a runner has no reporter and drops those lines). Read tokens from `request.Env`, not from the process environment. A lease is data: only `Id`, `CdpEndpoint`, and `ReconnectEndpoint` travel to a worker, and `ReleaseAsync` gets back the object `AcquireAsync` returned, so a provider can derive from `BrowserLease` and keep its own fields on it. Every lease is released exactly once, by the side that acquired it.

`BrowserProviderScope.Worker`, the default, leases one browser per worker slot when the run starts, all slots at once, and each worker attaches to its own for every attempt it runs. A runner calls `WebEngine.PrepareAsync` once per target before any worker starts, `Init` in each worker, and `FinishAsync` after the last worker ended. Every lease granted is held, so a slot that fails to lease ends the run before any test and the leases that succeeded are still released. When a worker's browser drops, the worker leases a replacement itself at its next attempt and releases it when the worker is disposed (`DisposeAsync`); the dead lease stays with the runner, which releases it in `FinishAsync`. A worker that dies cannot release what it acquired. Implement `SweepAsync` to have `FinishAsync` release those too: it runs once per target after the leases the runner holds were released, finds every lease tagged with the run id and the target at the service, releases it, and returns the ids, which are logged. A sweep that fails is logged and fails nothing.

`BrowserProviderScope.Attempt` leases a fresh browser at every attempt start, retries included, and releases it when the session is disposed, also after a start that failed. A lease that arrives once the attempt was cancelled or ended is given straight back. The attempt rides the browser's persistent default context, so the remote must contain only that context, and the limits of [`Connect.ReconnectEndpoint`](#recovering-a-cdp-transport) apply: no `Headers`, `BasicAuth`, `UserAgent`, `Locale`, or `TimezoneId` (`INVALID_CONFIG`), and no `App.ClearStateAsync` (`UNSUPPORTED_CAPABILITY`). After a transport drop the next operation reattaches through the lease's `ReconnectEndpoint`, or `CdpEndpoint` when it has none, and the engine checks that it is the same browser.

`Downloads` and `RecordAsync` are the two hooks for a service that holds the browser's files and screen. `Downloads` names a directory `Dir` on the browser's machine (an absolute POSIX or Windows path) and a `ReadAsync(lease, file, context, ct)` that returns the bytes of a finished file under it, so a download on a browser the runner cannot reach can be read back. `RecordAsync` starts the service's own recording of the leased browser and returns a `ProviderRecording`: its `StartedAt`, and `StopAsync`, which either writes a file into the directory it is given and returns `new ProviderRecordingResult { File = "replay.mp4" }`, or returns `{ Url, MediaType }` for a recording the service keeps. `ProviderRecordings.StopProviderRecordingAsync` ends a recording and checks it into a `VideoFile` (`video/<file>`) or a `VideoLink`, refusing a file that was not written, a non-`http(s)` URL, and a link without a media type. A provider that cannot record a browser returns null. The port has no `waitForDownload` and no video yet, so the engine does not call either hook; a provider can implement them now.

The test base classes need no runner. A `WebEngine` whose `Init` never ran leases for the one attempt it starts, reading the process environment, and gives the lease back when the session ends:

```csharp
protected override IEngine CreateEngine() => new WebEngine(new WebEngineOptions
{
    Browser = new HostedBrowsers("https://browsers.example.com"),
    Viewport = null,
});
```

A provider against a typical session API has three requests: create a session, poll it until it exposes a DevTools endpoint, stop it.

```csharp
public sealed class HostedBrowsers(string serviceUrl) : IBrowserProvider
{
    private static readonly HttpClient Http = new();

    public string Name => "hosted-browsers";

    public async Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        var token = request.Env.GetValueOrDefault("BROWSER_SERVICE_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("BROWSER_SERVICE_TOKEN is not set");
        }

        // 1. Ask for a browser; name the session after the run so it can be found later.
        var name = $"e2e {request.RunId} {request.TargetName} {request.AttemptId ?? $"slot {request.Slot + 1} of {request.Slots}"}";
        using var created = await Http.SendAsync(Authorized(HttpMethod.Post, $"{serviceUrl}/sessions", token, JsonContent.Create(new { name })), cancellationToken);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Session>(cancellationToken))!.Id;

        // 2. Wait until the session exposes its DevTools endpoint. A lease the engine never receives is never released, so stop the session on any failure.
        try
        {
            while (true)
            {
                using var response = await Http.SendAsync(Authorized(HttpMethod.Get, $"{serviceUrl}/sessions/{id}", token), cancellationToken);
                response.EnsureSuccessStatusCode();
                var session = (await response.Content.ReadFromJsonAsync<Session>(cancellationToken))!;
                if (session.Status == "ready" && session.CdpUrl is not null)
                {
                    return new BrowserLease { Id = id, CdpEndpoint = session.CdpUrl };
                }

                if (session.Status != "starting")
                {
                    throw new InvalidOperationException($"browser service: session {id} is {session.Status}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch
        {
            await StopAsync(id, token);
            throw;
        }
    }

    public async Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        // 3. Stop the session; the run waits for this within the cleanup timeout.
        await StopAsync(lease.Id, context.Env.GetValueOrDefault("BROWSER_SERVICE_TOKEN") ?? "", cancellationToken);
    }

    private async Task StopAsync(string id, string token, CancellationToken cancellationToken = default)
    {
        using var response = await Http.SendAsync(Authorized(HttpMethod.Post, $"{serviceUrl}/sessions/{id}/stop", token), cancellationToken);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, HttpContent? content = null) =>
        new(method, url) { Content = content, Headers = { Authorization = new("Bearer", token) } };

    private sealed record Session(string Id, string Status, string? CdpUrl);
}
```

### xUnit v3

`E2E.XUnit.V3.E2ETest` has the same members and reads the same config. Write `[Fact]` or `[Theory]` tests. It differs from the NUnit fixture in a few ways:

- `Expect.Soft` failures fail the test when it is disposed, in one `ASSERTION_FAILED`.
- xUnit has no retry, so every test is a first attempt and can replay.
- The session already stops on `Xunit.TestContext.Current.CancellationToken`, so a call does not need it. You can turn off the analyzer rule `xUnit1051` for E2E calls.
- `Expect.Poll` takes a predicate through `ToSatisfyAsync`. There is no `ToMatchAsync`.
- `E2E` and `Xunit` both have a `TestContext`. With both `using` lines, write `Xunit.TestContext.Current` for the xUnit one.

xUnit v2 is not supported. It cannot read a test's result during cleanup, and the replay cache needs that result.

## Config

`E2ETest` reads the nearest `e2e.config.json` above the test assembly directory, then above the working directory. The shape follows upstream `e2e.config.ts`. Every key is optional, and an unknown key fails with `INVALID_CONFIG`.

```json
{
  "targets": [{ "platform": "web", "app": { "url": "http://127.0.0.1:4173" } }],
  "timeout": 120000,
  "launchTimeout": 60000,
  "actionTimeout": 30000,
  "assertionTimeout": 5000,
  "cleanupTimeout": 30000,
  "agents": {
    "default": {
      "model": "gpt-4.1-mini",
      "baseUrl": "https://api.openai.com/v1",
      "apiKeyEnv": "OPENAI_API_KEY"
    }
  },
  "cache": { "mode": "read-write", "dir": ".e2e/cache", "strict": false },
  "secrets": { "stripe-key": null }
}
```

There is no default model and no shared API key. `baseUrl` can point at any OpenAI-compatible server, including a local one. Tests without agent steps ignore it.

`provider` picks who serves `model` and `judge`. Unset, it is `openai` (chat completions). `baseUrl` and `apiKeyEnv` default to the provider's own:

| `provider` | Key | Notes |
| --- | --- | --- |
| `openai` | `OPENAI_API_KEY` | Chat completions |
| `openai-responses` | `OPENAI_API_KEY` | The Responses API, as the AI SDK's `openai(id)` |
| `azure` | `AZURE_API_KEY` | Responses API at `https://$AZURE_RESOURCE_NAME.openai.azure.com/openai/v1`, or `baseUrl`. `model` is the deployment |
| `anthropic` | `ANTHROPIC_API_KEY` | Messages API, with prompt-cache breakpoints |
| `google` | `GOOGLE_GENERATIVE_AI_API_KEY` | Gemini API |
| `bedrock` | AWS credentials, or `AWS_BEARER_TOKEN_BEDROCK` | Converse API in `AWS_REGION` |
| `xai` | `XAI_API_KEY` | SpaceXAI API |
| `openrouter` | `OPENROUTER_API_KEY` | Model ids such as `openai/gpt-6-luna-fast` |
| `gateway` | `AI_GATEWAY_API_KEY`, else `VERCEL_OIDC_TOKEN` | Vercel AI Gateway |
| `openai-compatible` | `LLM_API_KEY`, optional | Any `/v1/chat/completions` server; `baseUrl` is required |
| `chatgpt`, `copilot`, `grok`, `opencode-console` | The stored login | Subscriptions, below |

An `apiKeyEnv` you set wins over the provider's default, so remove it when you switch providers. Otherwise the client reads the old variable, sends no key, and the provider answers 401.

`anthropic` needs a key scoped to a workspace (`sk-ant-api03-...`). A user key (`sk-ant-usr-...`) fails with a 400 that asks for an `anthropic-workspace-id` header, and the config cannot send one.

In code, `ModelProviders` builds the same clients (`ModelProviders.Anthropic("claude-sonnet-5")`), and `AnthropicModel`, `GoogleModel`, `BedrockModel`, `OpenAiResponsesModel`, and `OpenAiCompatibleModel` take full options.

### Subscriptions

A ChatGPT Plus/Pro, GitHub Copilot, OpenCode Console (Zen and Go), or SuperGrok/X Premium+ plan can serve the agent instead of an API key. Sign in once with the `e2e` tool:

```bash
dotnet tool install --global E2E.Cli
e2e login openai            # or github-copilot, opencode-console, spacexai
e2e models openai           # the model ids the plan serves
```

Then set `"provider": "chatgpt"` (or `copilot`, `opencode-console`, `grok`) with one of those ids, or use `Subscriptions.ChatGpt("gpt-6-luna")` in code. `e2e login openai --device` works without a browser, and `e2e login github-copilot` reuses `gh auth token` (or takes `--client-id` of your own OAuth App, and `--enterprise-url` for GitHub Enterprise). The login is stored in `~/.config/e2e/oauth.json`, the file upstream's `npx e2e login` writes, so a login made with either tool serves both. `E2E_OAUTH_CREDENTIALS` holding that JSON stands in for the file, and `OPENCODE_API_KEY` (a Console service account API key or an OpenCode Go subscription API key) replaces an OpenCode Console login, for the model and for `e2e models opencode-console`. Use API keys in CI.

A Claude Pro or Max plan cannot serve the agent. Anthropic allows that login only in its own apps. To run Claude on a subscription, use GitHub Copilot. Copilot writes model ids with a dot (`claude-sonnet-5.5`), where the Anthropic API uses a dash (`claude-sonnet-5-5`), so take the id from `e2e models github-copilot`:

```json
"agents": { "default": { "provider": "copilot", "model": "claude-sonnet-5.5" } }
```

From a clone of this repo, run the tool with `dotnet run --project src/E2E.Cli -- login github-copilot`.

An agent entry also takes `judge` (the model id for `assert`, `waitFor`, and `extract`), `system`, `context`, `maxSteps`, `maxModelCalls`, `judgmentTimeout`, and `providerOptions`. `agents` can name more agents than `default`; a call picks one with its `Agent` option, such as `new ActOptions { Agent = "careful" }`. Project tools (`AgentTools` on the fixture) are code, so they are not config keys; see topic `agent`. So is a custom executor (`AgentExecutor` on the fixture, `AgentOptions.Executor`), which replaces the built-in agent for `act` and `assert` with a `ToolLoop` of your own tools or your own `IStepExecutor`; see topic `agent`.

`cache.mode` is `off`, `read-only`, or `read-write`. Unset, it is `read-write` locally and `read-only` when `CI` is set. `cache.strict` fails a recording that no longer matches with `REPLAY_STALE` instead of running the step live. Under `cache.strict`, retries replay too, and the run never writes the cache. `cache.dir` resolves against the config file's directory. `E2E_CACHE_DIR` replaces it, so checkouts and worktrees of one project can share a replay cache without editing the config; the mode stays as configured.

Each secret reads `E2E_SECRET_<NAME>` first, then the config value. `null` means the variable is required. A test gets one with `Secrets.Get("stripe-key")`. Secret values passed to an act never enter cache entries: a name or test id that shows one is stored as `<secret:name>`.

A fixture overrides any value with the matching property, such as `BaseUrl`, `CacheMode`, or `ActionTimeout`, or replaces the whole config by overriding `Config`.

## Visual comparison

`ToHaveScreenshotAsync` compares what the app shows against a PNG stored next to the test file. It works on the web engine only; `DocumentEngine` fails with `UNSUPPORTED_CAPABILITY`.

```csharp
await Expect.That(Screen).ToHaveScreenshotAsync("checkout.png");
await Expect.That(Screen.GetByRole("button", "Pay")).ToHaveScreenshotAsync("pay-button.png");
await Expect.That(Screen).ToHaveScreenshotAsync("feed.png", new ScreenshotOptions
{
    Mask = [Screen.GetByTestId("timestamp")],
    MaxDiffPixels = 50,
});
```

`Expect.That(Screen)` compares the whole viewport. `Expect.That(locator)` compares the box of its one match, scrolled into view first. Without a name the screenshot is named after the test, `my-test-title-1`.

- **Stored at** `<test file>-snapshots/<name>-<target>-<os>.png`, for example `CheckoutTests.cs-snapshots/checkout-web-linux.png`. The test file is the file the call is written in. The OS is `darwin`, `linux`, or `win32`. Commit the files.
- **Missing.** The first run writes the screenshot and fails. Look at it, commit it, and run again. In CI (`CI` set) nothing is written into the project: the screenshot is attached under `.e2e/results/` at `snapshots/<path in the project>`, so it can be downloaded and committed.
- **Update.** `E2E_UPDATE_SNAPSHOTS=1 dotnet test` writes each missing or different screenshot and passes. A fixture can also override `UpdateSnapshots`. Screenshots are per OS, so write CI's from a CI job.
- **Different.** The matcher captures again until one matches or the assertion timeout passes, then fails with the number of differing pixels and three images under `.e2e/results/<test>/attempt-<n>/screenshots/`: `-diff`, `-actual`, and `-expected`.
- **Options.** `Threshold` (0 to 1, default 0.2), `MaxDiffPixels`, `MaxDiffPixelRatio` (the smaller wins when both are set), `Mask`, `MaskColor` (`#rrggbb`, default `#ff00ff`), and `Timeout`. `.Not` passes once the screen differs from a stored screenshot.
- **Secrets.** Secure fields are covered in every screenshot. After a test fills a `Secret`, the matcher fails with `POLICY_DENIED`.

## Coding agents (MCP)

`e2e mcp` serves the project to a coding agent such as Claude Code or Cursor over MCP (stdio). Register it once:

```bash
claude mcp add e2e -- e2e mcp
```

The server has four fixed tools (`open_session`, `tools`, `call`, `close_session`) and serves the skill as the resources `e2e://guide` and `e2e://guide/<topic>`. `open_session` boots a Chromium browser on the config's target; `call` then observes, acts, fills a secret, locates, and takes a screenshot, with every secret value shown by name. A session ends after 30 minutes idle or 4 hours, and nothing it does is written to the replay cache. The flags are `--config`, `--target`, `--headed`, and `--max-sessions` (1 through 16, default 4). See [COMPATIBILITY.md](COMPATIBILITY.md#mcp-server).

### A verification skill for your app

The e2e skill teaches an agent the runner. It does not know how your app starts, which page proves a feature, or what is not a bug there. The `create-verification-skill` skill writes that as a project-local `.agents/skills/verify-<app>/` skill: how to start the app, a smoke test as the check, the two ways to drive it (a live `e2e mcp` session, a test), what evidence a proof keeps, and a feature map with a bug-bash goal per feature. Install it, then ask your agent for `/create-verification-skill`:

```bash
npx skills add hardkoded/e2e-dotnet --skill create-verification-skill
```

## Sample

The sample is a real end-to-end test of [Dariten](https://dariten.vercel.app), a public personal finance demo. It drives Chromium through `WebEngine`, which installs the browser on its first run. The demo data is shared and anyone can edit it, so the tests only read the app and never check specific balances.

- `The_dashboard_links_to_the_transaction_register` uses locators only. It needs no model.
- `An_agent_filters_the_register_by_category` asks the agent to filter the register, then judges the result. It runs `claude-sonnet-5.5` on a GitHub Copilot plan and is in the `RealModel` category, which CI skips. A second passing run replays the filter without asking the model to act.

Sign in once with the GitHub CLI already logged in, then run the sample:

```bash
dotnet run --project src/E2E.Cli -- login github-copilot
dotnet test --project samples/E2E.Sample
```

To use an API key instead, change `agents.default` in `samples/E2E.Sample/e2e.config.json`, for example to `"provider": "openai", "model": "gpt-4.1-mini"` with `OPENAI_API_KEY` set.

[`samples/E2E.XUnit.V3.Sample`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/E2E.XUnit.V3.Sample) has the same two tests for xUnit v3. Run it with `dotnet test --project samples/E2E.XUnit.V3.Sample`.

Runs are headless. To watch a run in a browser window, set `E2E_HEADLESS=0`, for example `E2E_HEADLESS=0 dotnet test --project samples/E2E.Sample`.

[`samples/TodoMvc`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/TodoMvc) ports Playwright's TodoMVC example to agent steps, using the NuGet packages. Its README compares the two.

[`samples/MobileApp`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp) is a small .NET MAUI app, and [`samples/MobileApp.E2ETests`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp.E2ETests) drives it on an iOS simulator or Android emulator with `MobileEngine`. Its README lists the prerequisites (Node 22.12 or newer for agent-device) and the run commands.

## Tests

```bash
dotnet test E2E.slnx -c Release
```

Most unit tests use `DocumentEngine` and a scripted model. No test needs an API key. The Playwright tests install Chromium on the first launch, so the first run needs network access.

## What is in this port

| JavaScript | .NET |
| --- | --- |
| `e2e` test, expect, agent, cache, and `@e2e-dev/web` | `E2E` (`WebEngine`) |
| NUnit | `E2E.NUnit` (`E2ETest`) |
| xUnit v3 | `E2E.XUnit.V3` (`E2ETest`) |
| `@e2e-dev/eas` | `E2E.Eas` (`EasSimulators`) |
| `@e2e-dev/kernel` | `E2E.Kernel` (`KernelProvider`), hosted browsers for `WebEngineOptions.Browser` |
| `@e2e-dev/limrun` | `E2E.Limrun` (`LimrunSimulators`) |
| none (TestMu AI has no upstream package) | `E2E.TestMu` (`TestMuBrowsers`), hosted browsers for `WebEngineOptions.Browser` |
| `e2e login`, `e2e logout`, `e2e models`, `e2e guide`, `e2e mcp`, `e2e-web install` (as `e2e install`) | `E2E.Cli` (the `e2e` .NET tool) |

`@e2e-dev/mobile` is ported in part (the snapshot model and the engine) as `E2E.Mobile`. The EAS and Limrun providers are their own packages, `E2E.Eas` and `E2E.Limrun`. `E2E.TestMu` has no upstream counterpart. The Kernel provider is its own package, `E2E.Kernel`. `@e2e-dev/github` is not ported. Details are in [COMPATIBILITY.md](COMPATIBILITY.md).
