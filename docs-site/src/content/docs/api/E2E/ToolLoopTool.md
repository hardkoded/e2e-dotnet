---
title: "ToolLoopTool"
description: "One tool of a loop's vocabulary: what the model is told, and what runs when it calls it. Upstream's tools are AI SDK tools, which the port has no counterpart..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ToolLoopTool"
  hidden: true
---

<a id="E2E_ToolLoopTool"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ToolLoopTool : IEquatable<ToolLoopTool>
```


One tool of a loop's vocabulary: what the model is told, and what runs when it calls it. Upstream's tools are AI SDK tools,
which the port has no counterpart of, so this is the .NET shape of one entry of its `ToolSet`. A tool that throws
is read by the model as a failed call, except a hard stop of the step, which ends the loop.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ToolLoopTool&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L86)

## Constructors

<a id="E2E_ToolLoopTool__ctor_"></a>
<a id="E2E_ToolLoopTool__ctor_E2E_ModelTool_System_Func_E2E_ModelToolCall_System_Threading_CancellationToken_System_Threading_Tasks_Task_E2E_ToolLoopResult___"></a>

### `ToolLoopTool(ModelTool, Func<ModelToolCall, CancellationToken, Task<ToolLoopResult>>)`


```csharp
public ToolLoopTool(ModelTool Definition, Func<ModelToolCall, CancellationToken, Task<ToolLoopResult>> ExecuteAsync)
```


One tool of a loop's vocabulary: what the model is told, and what runs when it calls it. Upstream's tools are AI SDK tools,
which the port has no counterpart of, so this is the .NET shape of one entry of its `ToolSet`. A tool that throws
is read by the model as a failed call, except a hard stop of the step, which ends the loop.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Definition` | [ModelTool](/api/E2E/ModelTool/) | The name, the description, and the input schema the model sees. |
| `ExecuteAsync` | [Func&lt;ModelToolCall, CancellationToken, Task&lt;ToolLoopResult&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3) | Runs the call and answers what the model reads. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L86)

## Properties

<a id="E2E_ToolLoopTool_Definition_"></a>
<a id="E2E_ToolLoopTool_Definition"></a>

### Definition


```csharp
public ModelTool Definition { get; init; }
```


The name, the description, and the input schema the model sees.

**Value:** [ModelTool](/api/E2E/ModelTool/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L86)

<a id="E2E_ToolLoopTool_ExecuteAsync_"></a>
<a id="E2E_ToolLoopTool_ExecuteAsync"></a>

### ExecuteAsync


```csharp
public Func<ModelToolCall, CancellationToken, Task<ToolLoopResult>> ExecuteAsync { get; init; }
```


Runs the call and answers what the model reads.

**Value:** [Func&lt;ModelToolCall, CancellationToken, Task&lt;ToolLoopResult&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L86)
