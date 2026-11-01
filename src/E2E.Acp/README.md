# E2E.Acp

Run `Agent.ActAsync` and `Agent.AssertAsync` of [E2E for .NET](https://github.com/hardkoded/e2e-dotnet) on a coding agent you already use, such as Claude Code, Codex, or Cursor, over the [Agent Client Protocol](https://agentclientprotocol.com). The agent signs in with its own login or API key, so e2e needs no model provider.

```csharp
using E2E.Acp;

protected override IStepExecutor? AgentExecutor => AcpExecutors.ClaudeCode(new AcpAgentOptions { Model = "sonnet" });
```

`AcpExecutors.ClaudeCode()` and `AcpExecutors.Codex()` start the adapter installed in your project (`npm install -D @agentclientprotocol/claude-agent-acp` or `@agentclientprotocol/codex-acp`), so `node` must be on the path. `AcpExecutors.Acp(new AcpOptions { Command = "agent", Args = ["acp"] })` starts any ACP agent on stdio.

The agent gets e2e's step tools as its only tools. The runner records every action, so steps replay from the cache without the agent. See [Your coding agent](https://github.com/hardkoded/e2e-dotnet#your-coding-agent) and [COMPATIBILITY.md](https://github.com/hardkoded/e2e-dotnet/blob/main/COMPATIBILITY.md#acp-executor).

Not an official TesterArmy product.
