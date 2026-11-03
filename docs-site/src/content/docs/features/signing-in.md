---
title: Signing in / sessions
description: Sign in to a model plan, save a signed-in test session, or host sessions in your own runner.
sidebar:
  label: Signing in / sessions
---

## Subscriptions

A ChatGPT Plus/Pro, GitHub Copilot, OpenCode Console (Zen and Go), or SuperGrok/X Premium+ plan can serve the agent instead of an API key. Sign in once with the `e2e` tool:

```bash
dotnet tool install --global E2E.Cli
e2e login github-copilot    # or openai, opencode-console, spacexai
e2e models github-copilot   # the same provider id as the login
```

Then set `"provider": "chatgpt"` (or `copilot`, `opencode-console`, `grok`) with one of those ids, or use `Subscriptions.ChatGpt("gpt-6-luna")` in code. `e2e login openai --device` works without a browser, and `e2e login github-copilot` reuses `gh auth token` (or takes `--client-id` of your own OAuth App, and `--enterprise-url` for GitHub Enterprise). The login is stored in `~/.config/e2e/oauth.json`, the file upstream's `npx e2e login` writes, so a login made with either tool serves both. `E2E_OAUTH_CREDENTIALS` holding that JSON stands in for the file, and `OPENCODE_API_KEY` (a Console service account API key or an OpenCode Go subscription API key) replaces an OpenCode Console login, for the model and for `e2e models opencode-console`. Use API keys in CI.

A Claude Pro or Max plan cannot serve the agent. Anthropic allows that login only in its own apps. To run Claude on a subscription, use GitHub Copilot. Copilot writes model ids with a dot (`claude-sonnet-5.5`), where the Anthropic API uses a dash (`claude-sonnet-5-5`), so take the id from `e2e models github-copilot`:

```json
"agents": { "default": { "provider": "copilot", "model": "claude-sonnet-5.5" } }
```

From a clone of this repo, run the tool with `dotnet run --project src/E2E.Cli -- login github-copilot`.

## Saved sessions

Sign in once, then start other tests signed in. Mark the sign-in test `[E2ESetup("title", Saves = ["admin"])]` instead of `[Test]`. Its body signs in, checks that it worked, and calls `await SaveSessionAsync("admin")` once for each name in `Saves`. Mark a test or a class `[E2ESession("admin")]` to restore that session before the body runs.

```csharp
public sealed class Dashboard : E2ETest
{
    [E2ESetup("authenticate as admin", Saves = ["admin"])]
    public async Task Authenticate()
    {
        var admin = Credentials.User("admin");
        await App.OpenAsync("/login");
        await Screen.GetByLabel("Username").FillAsync(admin.Username);
        await Screen.GetByLabel("Password").FillAsync(admin.Password);
        await Screen.GetByRole("button", "Sign in").TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/dashboard");
        await SaveSessionAsync("admin");
    }

    [Test]
    [E2ESession("admin")]
    public async Task Opens_the_dashboard_directly()
    {
        await App.OpenAsync("/dashboard");
    }
}
```

- A name saved twice, a name not in `Saves`, or a name in `Saves` the body never saved fails the setup test with `SESSION_CONTRACT`. `SaveSessionAsync` outside a setup test fails the same way. Nothing is saved when the setup test fails.
- NUnit picks the order, not the dependents. A setup test gets an early `[Order]`, so it runs before the other tests of its own fixture. Put the setup test and the tests that restore its sessions in one fixture, or give the fixtures an `[Order]` that runs the setup fixture first. A class-level `[E2ESession]` does not apply to the setup tests of that class. A test that restores a session nobody saved yet fails with `SESSION_UNAVAILABLE`. Unlike upstream's runner, NUnit runs a setup test even when no selected test needs its session.
- A restore leaves no page open: call `App.OpenAsync` first. A session holds what the browser keeps (cookies, local storage, IndexedDB), not server state.
- An engine without a state capability (`stateCapability` false in `report.json`) fails `SaveSessionAsync` and a restore with `UNSUPPORTED_CAPABILITY`. `WebEngine` has it.
- A session lasts one run. It is encrypted with a key held in memory and kept under `.e2e/sessions/<run id>`. Derive an `E2ERunFixture` to delete it when the run ends. The secrets the saving test used are redacted in the tests that restore it.
- Only the NUnit adapter has the attributes so far.

## Hosting sessions in your own runner

`E2EHost` opens isolated per-test sessions for a runner that is not NUnit or xUnit (upstream's `openHost`). It loads the config, starts the target's app command, prepares the engine once, and opens one session at a time.

```csharp
await using var host = await E2EHost.OpenAsync(new OpenHostOptions { Cwd = projectDir });
await using var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "home screen" });
await session.Fixtures.App.OpenAsync("/");
```

`OpenHostOptions.Config` takes an `E2EConfig` value instead of a file; `EngineFactory` builds the engine of a target, because a config file holds no engine. `session.CloseAsync()` returns the soft assertion, abandoned-step (`STEP_NOT_AWAITED`), and cleanup failures; `DisposeAsync` throws them as an `AggregateException`. `session.Steps()` and `OpenSessionOptions.OnStep` report the steps as `E2E.Reports` types.

