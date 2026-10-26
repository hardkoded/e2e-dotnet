---
title: "DefaultAgent"
description: "Upstream's createBuiltInAgent (agent/default-agent.ts): the tool loop with the built-in rules, tools, and opening prompt."
editUrl: false
prev: false
next: false
sidebar:
  label: "DefaultAgent"
  hidden: true
---

<a id="E2E_DefaultAgent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class DefaultAgent
```


Upstream's `createBuiltInAgent` (`agent/default-agent.ts`): the tool loop with the built-in rules, tools, and opening prompt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/DefaultAgent.cs#L24)

## Methods

<a id="E2E_DefaultAgent_CreateBuiltInAgent_"></a>
<a id="E2E_DefaultAgent_CreateBuiltInAgent_E2E_BuiltInAgentOptions_"></a>

### CreateBuiltInAgent(BuiltInAgentOptions?)


```csharp
public static IStepExecutor CreateBuiltInAgent(BuiltInAgentOptions? options = null)
```


Builds the built-in step executor: the model loop with the grammar tools of the target's engine and the project
tools. It drives the step's own context, so it runs only where the runtime builds one, as a step's executor or inside a
custom executor that hands its context on.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [BuiltInAgentOptions](/api/E2E/BuiltInAgentOptions/) |  |

**Returns:** [IStepExecutor](/api/E2E/IStepExecutor/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/DefaultAgent.cs#L41)
