---
title: "VerdictTool"
description: "Upstream's VerdictTool: the conclusion tool, the policy that accepts or refuses the verdict a call carries, and the verdict it collected. Made by CreateVerdi..."
editUrl: false
prev: false
next: false
sidebar:
  label: "VerdictTool"
  hidden: true
---

<a id="E2E_VerdictTool"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class VerdictTool
```


Upstream's `VerdictTool`: the conclusion tool, the policy that accepts or refuses the verdict a call carries, and the verdict it
collected. Made by [CreateVerdictTool()](/api/E2E/AgentTools/#E2E_AgentTools_CreateVerdictTool); the tool loop adds one to every vocabulary, so an executor of its own that
drives a model outside the loop is the only caller of [Execute(ModelToolCall)](#E2E_VerdictTool_Execute_E2E_ModelToolCall_).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTools.cs)

## Fields

<a id="E2E_VerdictTool_Name"></a>

### Name


```csharp
public const string Name = "done"
```


The name the model calls to end a step.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

<a id="E2E_VerdictTool_Concluded_"></a>
<a id="E2E_VerdictTool_Concluded"></a>

### Concluded


```csharp
public bool Concluded { get; }
```


True once a verdict landed. Upstream's `concluded()`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_VerdictTool_Tool_"></a>
<a id="E2E_VerdictTool_Tool"></a>

### Tool


```csharp
public ModelTool Tool { get; }
```


The `done` tool; offer it beside the grammar tools.

**Value:** [ModelTool](/api/E2E/ModelTool/)

<a id="E2E_VerdictTool_Verdict_"></a>
<a id="E2E_VerdictTool_Verdict"></a>

### Verdict


```csharp
public StepVerdict? Verdict { get; }
```


The verdict once the model called the tool with an accepted one. Upstream's `verdict()`.

**Value:** `StepVerdict?`

## Methods

<a id="E2E_VerdictTool_Execute_"></a>
<a id="E2E_VerdictTool_Execute_E2E_ModelToolCall_"></a>

### Execute(ModelToolCall)


```csharp
public ToolLoopResult Execute(ModelToolCall call)
```


Runs one call of the tool and answers what the model reads. The first accepted verdict is final. A code sent beside `passed` is
dropped and the answer says so, never refused.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [ModelToolCall](/api/E2E/ModelToolCall/) |  |

**Returns:** [ToolLoopResult](/api/E2E/ToolLoopResult/)
