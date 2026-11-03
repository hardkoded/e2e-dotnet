# E2E.Acp

Runs `agent.act` and `agent.assert` steps of [`E2E`](https://www.nuget.org/packages/E2E) on any coding agent that speaks the [Agent Client Protocol](https://agentclientprotocol.com) on stdio, part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/acp`. Not an official TesterArmy product.

```
dotnet add package E2E.Acp
```

`AcpExecutor.Create` starts the agent you name, opens one session per test attempt, and gives it the built-in agent's action tools and its `done` verdict tool as an MCP server on `127.0.0.1`. Every action goes through the step's own actions, so the runner authorizes, counts, and records it, and cached steps replay as the built-in agent's do.

```csharp
using E2E.Acp;

IStepExecutor claude = AcpExecutor.ClaudeCode(new AcpAgentOptions { Model = "sonnet" });
IStepExecutor codex = AcpExecutor.Codex();
IStepExecutor other = AcpExecutor.Create("agent", ["acp"]);
```

`ClaudeCode` and `Codex` start the agent through its npm adapter, found in `node_modules` from the working directory up and run with `node`, so Node must be on the `PATH`: `npm install -D @agentclientprotocol/claude-agent-acp` or `@agentclientprotocol/codex-acp`. They turn off the agent's own tools, settings, and MCP servers, so the step tools are all it has. Sign in with `claude` or `codex login`. The agent's environment has no `E2E_SECRET_*` or `E2E_USER_*` variable. On Windows, a command for `Create` must be an executable, not a `.cmd` shim.

The agent signs in the way it does on its own. The client advertises no file system or terminal, and it denies every permission request that is not for one of the step tools. A step where the agent runs a tool of its own without asking fails with `POLICY_DENIED`.

`AcpAgentOptions` takes `Env`, `Model` (a value of the session's `model` option), `System` (appended to the built-in rules), and `Name`. `AcpOptions`, for `Create`, adds `Mode` and `SessionMeta` (`_meta` of `session/new`).

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
