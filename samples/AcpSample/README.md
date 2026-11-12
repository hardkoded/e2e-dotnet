# ACP sample

This sample runs the steps of the [TodoMVC demo](https://demo.playwright.dev/todomvc) on a coding agent over the Agent Client Protocol, with `AcpExecutor.ClaudeCode` or `AcpExecutor.Codex` from the `E2E.Acp` package. The agent signs in with its own login, so the sample needs no model provider. It is the live check for the presets, run by hand: no CI job runs it.

## Run it

Node must be on the `PATH`. The presets run the agent's adapter with `node`, found in `node_modules` from the working directory up.

```bash
cd samples/AcpSample
npm install
claude   # sign in once, or set ANTHROPIC_API_KEY
dotnet test
```

For Codex, run `codex login`, then `ACP_AGENT=codex dotnet test`. `ACP_MODEL` picks the Claude model (`sonnet` by default).

The tests are in the `AcpAgent` category, not `RealModel`: they need your Claude or Codex login, and CI skips them. Run them by hand with `dotnet test --filter "TestCategory=AcpAgent"`. The first run asks the agent for each `ActAsync` and records it in `.e2e/cache`. The next run replays those steps without the agent. `AssertAsync` always reaches it.
