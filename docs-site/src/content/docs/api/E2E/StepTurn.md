---
title: "StepTurn"
description: "One model turn of an agent step: the tool calls the model made and what came back, each bounded (upstream's StepTurn)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepTurn"
  hidden: true
---

<a id="E2E_StepTurn"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record StepTurn : IEquatable<StepTurn>
```


One model turn of an agent step: the tool calls the model made and what came back, each bounded (upstream's `StepTurn`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepTurn&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L166)

## Constructors

<a id="E2E_StepTurn__ctor_"></a>
<a id="E2E_StepTurn__ctor_System_Int32_System_Collections_Generic_IReadOnlyList_System_String__System_String_"></a>

### `StepTurn(int, IReadOnlyList<string>, string)`


```csharp
public StepTurn(int Index, IReadOnlyList<string> Calls, string Outcome)
```


One model turn of an agent step: the tool calls the model made and what came back, each bounded (upstream's `StepTurn`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | One-based turn number within the step. |
| `Calls` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | `tap({"target":"n19"})`, one per tool call, arguments clipped. |
| `Outcome` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The tool results of the turn, clipped. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L166)

## Properties

<a id="E2E_StepTurn_Calls_"></a>
<a id="E2E_StepTurn_Calls"></a>

### Calls


```csharp
public IReadOnlyList<string> Calls { get; init; }
```


`tap({"target":"n19"})`, one per tool call, arguments clipped.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L166)

<a id="E2E_StepTurn_Index_"></a>
<a id="E2E_StepTurn_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


One-based turn number within the step.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L166)

<a id="E2E_StepTurn_Outcome_"></a>
<a id="E2E_StepTurn_Outcome"></a>

### Outcome


```csharp
public string Outcome { get; init; }
```


The tool results of the turn, clipped.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L166)
