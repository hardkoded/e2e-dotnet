---
title: "PreparedTurn"
description: "What PrepareMessages learns about the turn that is about to run."
editUrl: false
prev: false
next: false
sidebar:
  label: "PreparedTurn"
  hidden: true
---

<a id="E2E_PreparedTurn"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record PreparedTurn : IEquatable<PreparedTurn>
```


What [PrepareMessages](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_PrepareMessages) learns about the turn that is about to run.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PreparedTurn&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L144)

## Constructors

<a id="E2E_PreparedTurn__ctor_"></a>
<a id="E2E_PreparedTurn__ctor_System_Int32_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `PreparedTurn(int, IReadOnlyList<string>)`


```csharp
public PreparedTurn(int StepNumber, IReadOnlyList<string> PreviousToolCalls)
```


What [PrepareMessages](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_PrepareMessages) learns about the turn that is about to run.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `StepNumber` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The zero-based index of the turn about to run. |
| `PreviousToolCalls` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The names of the tools the previous turn called, in order. Empty on the first turn. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L144)

## Properties

<a id="E2E_PreparedTurn_PreviousToolCalls_"></a>
<a id="E2E_PreparedTurn_PreviousToolCalls"></a>

### PreviousToolCalls


```csharp
public IReadOnlyList<string> PreviousToolCalls { get; init; }
```


The names of the tools the previous turn called, in order. Empty on the first turn.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L144)

<a id="E2E_PreparedTurn_StepNumber_"></a>
<a id="E2E_PreparedTurn_StepNumber"></a>

### StepNumber


```csharp
public int StepNumber { get; init; }
```


The zero-based index of the turn about to run.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L144)
