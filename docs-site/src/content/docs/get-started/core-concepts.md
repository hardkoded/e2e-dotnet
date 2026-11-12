---
title: Core concepts
description: What a test is, how config names the app and the model, and how xUnit v3 differs.
sidebar:
  label: Core concepts
---

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

`E2ETest` starts a `WebEngine` session for each `[Test]`. Override `CreateEngine` with a `DocumentEngine` when the test should not open a browser, or with a `WebEngine` whose `Connect` attaches to a remote Chromium over CDP (see [Recovering a CDP transport](/features/browser/#recovering-a-cdp-transport)), or whose `Browser` leases hosted browsers (see [Hosted browsers](/features/browser/#hosted-browsers)). An `act` that a later `assert`, `waitFor`, or locator `Expect` verifies is recorded. The next run replays those actions with no model calls until the screen no longer matches. See [Self-healing tests](/features/caching/#self-healing-tests). In `read-write` mode, when the test ends, verified acts are written and unverified acts that recorded or replayed are evicted, whether it passed, failed, or was skipped. A model outage leaves existing entries in place, unless a failure on the app implicated them too, and an `E2ETest` run whose cancellation token was cancelled writes and evicts nothing. A `Standalone` attempt that is disposed without completion still writes and evicts, even when its token was cancelled. `read-only` and `cache.strict` never write or evict. `[Retry]` runs the later attempts live, and they still record. Tests that never call the agent need no model. A judgment (`AssertAsync`, `WaitForAsync`, `ExtractAsync`) reads the text snapshot of the screen and where it is: the path on the app, or the whole address off it. The address is evidence only for a claim about the URL or route, and a `WaitForAsync` judges again when the address changes, unless `Vision = VisionMode.Only` withholds the tree. Pass `Vision = VisionMode.On` to show it a masked screenshot too, or `VisionMode.Only` for the screenshot alone, when the answer is in pixels. After a test fills a `Secret`, no pixels reach a model. Screenshots kept as artifacts stay on and mask secure fields only.

### xUnit v3

`E2E.XUnit.V3.E2ETest` has the same members and reads the same config. Write `[Fact]` or `[Theory]` tests. It differs from the NUnit fixture in a few ways:

- `Expect.Soft` failures fail the test when it is disposed, in one `ASSERTION_FAILED`.
- xUnit has no retry, so every test is a first attempt and can replay.
- The session already stops on `Xunit.TestContext.Current.CancellationToken`, so a call does not need it. You can turn off the analyzer rule `xUnit1051` for E2E calls.
- `Expect.Poll` takes a predicate through `ToSatisfyAsync`. There is no `ToMatchAsync`.
- `E2E` and `Xunit` both have a `TestContext`. With both `using` lines, write `Xunit.TestContext.Current` for the xUnit one.

xUnit v2 is not supported. It cannot read a test's result during cleanup, and the replay cache needs that result.

### TUnit

`E2E.TUnit.E2ETest` has the same members and reads the same config. Write `[Test]` methods, with `[Arguments]` for data rows. TUnit builds a new instance of the class for each test. It differs from the NUnit fixture in a few ways:

- A before-test hook starts the session, and an after-test hook commits the replay cache. Your own `[After(Test)]` hook runs before the commit.
- `Expect.Soft` failures fail the test when its after-test hook runs, in one `ASSERTION_FAILED`. A soft failure before `Skip.Test(...)` fails the test.
- The first attempt can replay. `[Retry]` attempts run live and still record.
- The session stops on the test's cancellation token, so a call does not need it.
- `Expect.Poll` takes a predicate through `ToSatisfyAsync`. There is no `ToMatchAsync`.
- `E2ERunFixture` works as in the other adapters (reporters, app-command shutdown, `--changed-since`, `--last-failed`), but TUnit has no assembly fixture to register: derive a public class with a public parameterless constructor in the test project (`public sealed class AppCommands : E2ERunFixture;`) and a test-session hook finds it by reflection. A project with no such class runs none of it.
- Saved sessions: write `[Test]` and `[E2ESetup("title", Saves = ["admin"])]` on the sign-in test (TUnit does not let a library define a test attribute, so `[E2ESetup]` goes beside `[Test]`), and `[E2ESession("admin")]` on the tests that restore it. The setup test gets the highest execution priority. TUnit runs tests in parallel, so a setup test and the test that restores its session can start together: put `[NotInParallel]` on the class if the setup must not run twice.
- Mobile and Windows tests: `E2E.Mobile.TUnit` (`MobileAppTest`) and `E2E.WinApp.TUnit` (`WinAppTest`).
- On .NET SDK 10 and later, `dotnet test` needs `{ "test": { "runner": "Microsoft.Testing.Platform" } }` in a `global.json`, because TUnit runs on Microsoft.Testing.Platform. Filter with `dotnet test --treenode-filter "/*/*/*/*[Category=RealModel]"`, or run the project with `dotnet run`.

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

A call's own `Timeout` wins over `actionTimeout` and `assertionTimeout`. A `Timeout` of `TimeSpan.Zero` on a locator action, `WaitForAsync`, an `Expect` matcher, `ToHaveScreenshotAsync`, `Screen.TreeAsync`, `Screen.DebugAsync`, `Browser.GotoAsync`, `WaitForResponseAsync`, `WaitForDownloadAsync`, or `WaitForDownloadsAsync` sets no deadline of its own: the test timeout still bounds it. `Expect.Poll` with a zero `Timeout` polls until the cancellation token you pass ends, and without a token that can end it fails with `INVALID_CONFIG`. A negative `Timeout` is `INVALID_ARGUMENT` (`INVALID_CONFIG` from `Expect.Poll`, as is a zero or negative `Interval`). `Agent` calls take a positive `Timeout` only: zero or less is `INVALID_ARGUMENT`.

There is no default model and no shared API key. `baseUrl` can point at any OpenAI-compatible server, including a local one. Tests without agent steps ignore it.

`credentials` declares accounts: `"credentials": { "admin": { "username": "admin@example.com", "password": "..." } }`. `E2E_USER_ADMIN_USERNAME` and `E2E_USER_ADMIN_PASSWORD` win over the config and may replace either field. The password is the secret `admin.password` (at least 6 characters) and is redacted like any secret; a `secrets` entry cannot take that name. While a test runs, `Credentials.User(name)` returns the account the config declares; a name the config does not declare reads the `E2E_USER_*` variables alone. `e2e explore` signs in with every configured account. `E2EConfig.Credentials` gives the resolved accounts. A password, or a `secrets` entry, may be a provider the code sets instead of a value: `config.WithCredential("admin", "admin@example.com", ct => Task.FromResult(totp.Now()))` or `config.WithSecretProvider("stripe-key", ct => vault.ReadAsync("stripe", ct))`. The runner asks it on every fill, after the authorization checks pass, and redacts what it returns from then on. A value under 6 code points fails the fill, and the variables above still win over a provider.

`failOnSkippedFailure` (a boolean, false by default) and `maxDuration` (a positive integer of milliseconds, at most `2147483647`, unset by default) are accepted and checked as upstream does: `failOnSkippedFailure must be a boolean`, `maxDuration must be a positive safe integer of milliseconds, got ...`. The port has no runner, so it only carries them (`E2EConfig.FailOnSkippedFailure`, `E2EConfig.MaxDuration`); the test framework decides the exit code and when a run stops. Neither changes the replay cache.

A device test declares its app on the target, `"targets": [{ "platform": "ios", "app": { "bundleId": "com.example.app", "appPath": "build/App.app" } }]`, and derives from `MobileAppTest` of `E2E.Mobile.NUnit`, `E2E.Mobile.XUnit.V3`, or `E2E.Mobile.TUnit`.

An engine built in code replaces the platform's: `config.WithTarget(name, engine)` puts an `IEngine` in the target of that name (it keeps that target's app; its platform is the engine's). A run checks every secret the engine's options hold against `secrets` at load, in every target, so a typo fails with the nearest name, and the engine's name, version, platform, and capabilities enter the config digest.

`targets` may list several entries. Each has a unique `name`, which defaults to its platform. A fixture, `E2EHost.OpenAsync`, `e2e mcp`, and `e2e explore` that need one target of several name it: override `E2EFixture.TargetName`, pass `OpenHostOptions.Target`, or pass `--target`. Without a name, a host or an MCP session fails with `TARGET_REQUIRED`, and `e2e explore` takes the first target.

`trace` and `video` choose which attempts record: `off`, `on`, `retain-on-failure`, `on-first-retry`, or `on-all-retries`. They go at the config root or on the target, and `E2E_TRACE` and `E2E_VIDEO` replace them for one run. The defaults are `retain-on-failure` and `off`. The port records `video` on the web engine and writes a trace page, `trace.md`, for each attempt that keeps one (see [Trace page](/features/caching/#trace-page) and [Video](/features/video/#video)).

`E2E_DEBUG=1` (or `true`) is the port's `--debug`. Each test then writes a table of its phase timings (`session.launch`, `test.body`, `session.close`, `agent.observe`, `agent.model`, `agent.action`) to stderr when it ends, then a table with one row per agent step (time, model calls, tokens in and out, cache share, cost), and keeps the transcript of each agent step. See [COMPATIBILITY.md](/compatibility/#debug).

`E2E_AGENT=<name>` is the port's `--agent`: the agent that calls use when neither the call nor the test (`[Agent("name")]` on a class or method) names one. See [COMPATIBILITY.md](/compatibility/#agents).

`E2E_AI_TRACE=1` (or `true`) is the port's `--ai-trace`. Each test then records every model call, and the test process writes them to `.e2e/ai-trace-<process id>.json` in the shape `npx unbox-ai` reads. See [COMPATIBILITY.md](/compatibility/#ai-trace).

`provider` picks who serves `model` and `judge`. Unset, it is `openai` (chat completions). `baseUrl` and `apiKeyEnv` default to the provider's own:

| `provider` | Key | Notes |
| --- | --- | --- |
| `openai` | `OPENAI_API_KEY` | Chat completions |
| `openai-responses` | `OPENAI_API_KEY` | The Responses API, as the AI SDK's `openai(id)` |
| `azure` | `AZURE_API_KEY` | Responses API at `https://$AZURE_RESOURCE_NAME.openai.azure.com/openai/v1`, or `baseUrl`. `model` is the deployment |
| `anthropic` | `ANTHROPIC_API_KEY` | Messages API, with prompt-cache breakpoints |
| `google` | `GOOGLE_GENERATIVE_AI_API_KEY` | Gemini API |
| `bedrock` | `AWS_BEARER_TOKEN_BEDROCK`, or AWS credentials: `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, else the AWS credential chain (`AWS_PROFILE` or the default profile, SSO, container and instance roles) | Converse API in `AWS_REGION` |
| `xai` | `XAI_API_KEY` | SpaceXAI API |
| `openrouter` | `OPENROUTER_API_KEY` | Model ids such as `openai/gpt-6-luna-fast` |
| `gateway` | `AI_GATEWAY_API_KEY`, else `VERCEL_OIDC_TOKEN` | Vercel AI Gateway |
| `openai-compatible` | `LLM_API_KEY`, optional | Any `/v1/chat/completions` server; `baseUrl` is required |
| `chatgpt`, `copilot`, `grok`, `opencode-console` | The stored login | [Subscriptions](/features/signing-in/#subscriptions) |

An `apiKeyEnv` you set wins over the provider's default, so remove it when you switch providers. Otherwise the client reads the old variable, sends no key, and the provider answers 401.

`anthropic` needs a key scoped to a workspace (`sk-ant-api03-...`). A user key (`sk-ant-usr-...`) fails with a 400 that asks for an `anthropic-workspace-id` header, and the config cannot send one.

Claude Opus 5.5, Sonnet 5.5, and Fable 5.1 bind each thinking block to the system prompt and the messages before it, and reject a request that replays the block after either changed. An agent step keeps its prompt fixed, and once it rewrites an earlier message (an executor's `PrepareMessages` changed it, or the history was shrunk after a context overflow) it stops sending the thinking from that message on. The step's turns note it.

In code, `ModelProviders` builds the same clients (`ModelProviders.Anthropic("claude-sonnet-5")`), and `AnthropicModel`, `GoogleModel`, `BedrockModel`, `OpenAiResponsesModel`, and `OpenAiCompatibleModel` take full options.

An agent entry also takes `judge` (the model id for `assert`, `waitFor`, and `extract`; an `AssertAsync` with `Explore = true` uses `model` instead), `system`, `context`, `maxSteps`, `maxModelCalls`, `judgmentTimeout`, `maxObservationBytes` (the most bytes of the screen one model request carries, 1024 through 16777216, 262144 by default), `maxInputTokens` (the input one model request may carry, 1 through 1000000, 64000 by default; a dense screen is cut to fit under it), and `providerOptions`. `agents` can name more agents than `default`; a call picks one with its `Agent` option, such as `new ActOptions { Agent = "careful" }`. Project tools written as code (`AgentTools` on the fixture) are not config keys; see topic `agent`. A config can declare a project tool as a command, with `tools` in the agents entry. A command tool is an entry of `tools` in the agents entry: `{ "description": ..., "mutates": false, "command": ["program", "arg"], "inputSchema": {...}, "platforms": ["web"] }`. The tool runs the command in the project root with the model's input as JSON on stdin, answers what the command wrote on stdout, and fails the call when it exits with a code other than 0 or runs past two minutes. What it writes on stderr is printed on the server's stderr, with the config's secrets redacted. `e2e mcp` serves it beside the session's own tools. So is a custom executor (`AgentExecutor` on the fixture, `AgentOptions.Executor`), which replaces the built-in agent for `act` and `assert` with a `ToolLoop` of your own tools (the built-in actions are `AgentTools.CreateGrammarTools`) or your own `IStepExecutor`; see topic `agent`. So is the artifact store (`ArtifactStore` on the fixture, `E2ESessionOptions.ArtifactStore`): an `IArtifactStore` receives every artifact an attempt registers, a download or a video, as it is produced, with its bytes, digest, redaction label, and the run, test, attempt, and step it belongs to, and the reference it returns is recorded as the artifact's `ref`. A store that throws leaves the record with its local path and no `ref`; the run does not fail. See topic `setup`.

`ActOptions.OnAction` runs after each action of an `act`, replayed ones included, once the screen settles. Its `ActionEvent` has `Index`, `Action`, `Summary` (the prose the trace stores for the action, so the event, the trace and a replay hand-off notice read the same text), `Source`, and `ActionName` (an enum; `Tool` for a mutating project tool, with `ToolName`; `DismissKeyboard` for a keyboard hidden by the agent on a device; `Resize` for a viewport the agent resized with `resize_viewport`). A point action arrives as a `TapAtActionEvent` or `HoverAtActionEvent` with `Point`, or a `DragAtActionEvent` with `From` and `To`. Points are in the space of `Screen.TapAtAsync` and `BoundingBox`, rounded and clamped to the viewport, and a replay reports the points it dispatches on the current screen. Mobile engines have no `hoverAt` or `dragAt`.

```csharp
await Agent.ActAsync("move the pin to Warsaw", new ActOptions
{
    OnAction = e =>
    {
        if (e is DragAtActionEvent drag) Console.WriteLine($"{drag.From} -> {drag.To}");
        return Task.CompletedTask;
    },
});
```

`ActOptions.OnBeforeAction` runs before each action, replayed ones included, with a `BeforeActionEvent` (`Index`, the same one that action's `OnAction` event gets, and `Source`). Use it to capture the screen an action started on. The step waits for it, and it does not wait for the screen to settle first. If it throws, the step fails before the action runs, with a message that starts with `agent.act onBeforeAction failed`. An action can still fail after it, with no `OnAction` call.

```csharp
await Agent.ActAsync("open settings and turn on dark mode", new ActOptions
{
    OnBeforeAction = e => App.ScreenshotAsync($"before {e.Index}"),
});
```

Exploring an app and the package map stay on [the guide](/guide/). The repository sample is [`samples/E2E.Sample`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/E2E.Sample).
