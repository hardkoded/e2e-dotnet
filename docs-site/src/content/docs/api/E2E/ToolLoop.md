---
title: "ToolLoop"
description: "Upstream's createToolLoopExecutor (agent/tool-loop.ts): the chassis of an executor that thinks in a loop of model turns and tool calls. It owns everything bu..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ToolLoop"
  hidden: true
---

<a id="E2E_ToolLoop"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class ToolLoop
```


Upstream's `createToolLoopExecutor` (`agent/tool-loop.ts`): the chassis of an executor that thinks in a loop of model
turns and tool calls. It owns everything but the tool vocabulary: the `done` verdict tool and its closed code policy, the
hard stops (a spent budget, a timeout, or a cancel ends the loop, never the model), the loop guards, the shrink of a history
that overflowed the model, and the count of model calls. The built-in agent is this chassis plus the grammar tools
([CreateBuiltInAgent(BuiltInAgentOptions)](/api/E2E/DefaultAgent/#E2E_DefaultAgent_CreateBuiltInAgent_E2E_BuiltInAgentOptions_)). An executor for a device or an API brings other tools and inherits the same discipline.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L18)

## Methods

<a id="E2E_ToolLoop_CreateToolLoopExecutor_"></a>
<a id="E2E_ToolLoop_CreateToolLoopExecutor_E2E_ToolLoopExecutorOptions_"></a>

### CreateToolLoopExecutor(ToolLoopExecutorOptions)


```csharp
public static IStepExecutor CreateToolLoopExecutor(ToolLoopExecutorOptions options)
```


Builds a step executor from a tool vocabulary and the chassis. The loop runs at most
[MaxModelCalls](/api/E2E/IExecutorBudgets/#E2E_IExecutorBudgets_MaxModelCalls) model turns per step: the agents entry's `MaxModelCalls`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ToolLoopExecutorOptions](/api/E2E/ToolLoopExecutorOptions/) |  |

**Returns:** [IStepExecutor](/api/E2E/IStepExecutor/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L24)
