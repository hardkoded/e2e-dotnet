---
title: "AgentPrimitives"
description: "The exports of upstream's e2e/agent entrypoint (agent/public.ts, from agent/primitives.ts) that an executor keeping the built-in actions and verdicts builds..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentPrimitives"
  hidden: true
---

<a id="E2E_AgentPrimitives"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class AgentPrimitives
```


The exports of upstream's `e2e/agent` entrypoint (`agent/public.ts`, from `agent/primitives.ts`) that an executor
keeping the built-in actions and verdicts builds on: `CreateGrammarTools` and `CreateVerdictTool`, with
`VerdictRules`. `DefaultAgent.BaseRules` is upstream's `BASE_RULES` and `StepExecutors.IsRuntimeHardStop` its
`isRuntimeHardStop`. .NET-only: the holder class, which in upstream is a module.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L15)

## Fields

<a id="E2E_AgentPrimitives_VerdictRules"></a>

### VerdictRules


```csharp
public const string VerdictRules = "Verdict rules:\n- When the step's goal is achieved, or you are certain it cannot be, call done exactly once.\n- Conclude from the newest screen and changes already in this conversation; never guess success. Observe again before concluding only when the newest result shows work still in progress (a spinner, \"Saving…\", a pending state).\n- \"passed\" means the application behaved as the step required. \"failed\" means it did not. \"blocked\" means credentials, the environment, or test setup prevented a product verdict — blocked says nothing about the product and requires a code."
```


The verdict rules the built-in agent appends to its instructions. Upstream's `VERDICT_RULES`, for the port's
`done` tool where upstream names `complete_step`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L21)

## Methods

<a id="E2E_AgentPrimitives_CreateGrammarTools_"></a>
<a id="E2E_AgentPrimitives_CreateGrammarTools_E2E_StepExecutorContext_E2E_GrammarToolOptions_"></a>

### CreateGrammarTools(StepExecutorContext, GrammarToolOptions?)


```csharp
public static IReadOnlyList<GrammarTool> CreateGrammarTools(StepExecutorContext context, GrammarToolOptions? options = null)
```


The built-in agent's action tools for the verbs the context's target supports, each run through the step's own
[IExecutorActions](/api/E2E/IExecutorActions/), where the runtime authorizes, counts, and records the action so the step replays from the
cache. Upstream's `createGrammarTools`. The tools drive the context the runtime builds for a step, and throw
[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) on one built by hand.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `options` | [GrammarToolOptions](/api/E2E/GrammarToolOptions/) |  |

**Returns:** [IReadOnlyList&lt;GrammarTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L33)

<a id="E2E_AgentPrimitives_CreateVerdictTool_"></a>
<a id="E2E_AgentPrimitives_CreateVerdictTool"></a>

### CreateVerdictTool()


```csharp
public static VerdictTool CreateVerdictTool()
```


The conclusion tool and the closed policy that accepts or refuses its verdict. Upstream's `createVerdictTool`; the tool
is `done`, where upstream's is `complete_step`.

**Returns:** [VerdictTool](/api/E2E/VerdictTool/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L77)
