---
title: "AgentTools"
description: "The built-in agent's tool vocabulary (upstream's agent/primitives.ts). CreateGrammarTools and CreateVerdictTool() are public, for a tool loop or an executor..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentTools"
  hidden: true
---

<a id="E2E_AgentTools"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class AgentTools
```


The built-in agent's tool vocabulary (upstream's `agent/primitives.ts`). `CreateGrammarTools` and
[CreateVerdictTool()](#E2E_AgentTools_CreateVerdictTool) are public, for a tool loop or an executor that keeps the built-in actions and verdicts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTools.cs)

## Fields

<a id="E2E_AgentTools_VerdictRules"></a>

### VerdictRules


```csharp
public const string VerdictRules = "Finish with done when the step's goal is achieved, or you are certain it cannot be. done status is passed, failed, or blocked. blocked means credentials, the environment, or test setup prevented a verdict, and requires a code."
```


Upstream's `VERDICT_RULES`: the guidance for [VerdictTool](/api/E2E/VerdictTool/). The tool loop appends it to the instructions of every
executor it builds, so an executor outside the loop gives it to its own model beside the tool. The port's tool is `done`
where upstream's is `complete_step`, and takes `code` where upstream's takes `errorCode`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_AgentTools_CreateGrammarTools_"></a>
<a id="E2E_AgentTools_CreateGrammarTools_E2E_StepExecutorContext_E2E_GrammarToolsOptions_"></a>

### CreateGrammarTools(StepExecutorContext, GrammarToolsOptions?)


```csharp
public static IReadOnlyList<ToolLoopTool> CreateGrammarTools(StepExecutorContext context, GrammarToolsOptions? options = null)
```


Upstream's `createGrammarTools`: the built-in agent's action tools for the verbs the step's engine supports, each answering what
changed on screen. Offer them from [Tools](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_Tools) beside tools of your own, and begin the executor's
[System](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_System) with [BaseRules](/api/E2E/DefaultAgent/#E2E_DefaultAgent_BaseRules). Every call runs through the step's own actions, so it is
authorized, counted, recorded, and replayed from the cache. The context must be one the runtime opened for a step, as the built-in
agent's is. The screen presenter and the failure causes stay internal, as upstream's do.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `options` | [GrammarToolsOptions](/api/E2E/GrammarToolsOptions/) |  |

**Returns:** [IReadOnlyList&lt;ToolLoopTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_AgentTools_CreateVerdictTool_"></a>
<a id="E2E_AgentTools_CreateVerdictTool"></a>

### CreateVerdictTool()


```csharp
public static VerdictTool CreateVerdictTool()
```


Upstream's `createVerdictTool`: the `done` tool with the closed blocked-code policy, and the verdict it collected. Make a
fresh one for each step: the first accepted verdict is final.

**Returns:** [VerdictTool](/api/E2E/VerdictTool/)
