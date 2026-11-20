---
title: "DecisionAction"
description: "One action the loop dispatches, with everything its operation needs (upstream's Action)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionAction"
  hidden: true
---

<a id="E2E_Decision_DecisionAction"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class DecisionAction
```


One action the loop dispatches, with everything its operation needs (upstream's `Action`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Dispatch.cs)

## Properties

<a id="E2E_Decision_DecisionAction_Control_"></a>
<a id="E2E_Decision_DecisionAction_Control"></a>

### Control


```csharp
public Control? Control { get; init; }
```


The control, for a viewport, history, or keyboard action.

**Value:** `Control?`

<a id="E2E_Decision_DecisionAction_DestinationId_"></a>
<a id="E2E_Decision_DecisionAction_DestinationId"></a>

### DestinationId


```csharp
public string? DestinationId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_DecisionAction_Name_"></a>
<a id="E2E_Decision_DecisionAction_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_DecisionAction_Operation_"></a>
<a id="E2E_Decision_DecisionAction_Operation"></a>

### Operation


```csharp
public Operation? Operation { get; init; }
```


The operation, for an action on a target or a point.

**Value:** `Operation?`

<a id="E2E_Decision_DecisionAction_Paths_"></a>
<a id="E2E_Decision_DecisionAction_Paths"></a>

### Paths


```csharp
public IReadOnlyList<string>? Paths { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_DecisionAction_Point_"></a>
<a id="E2E_Decision_DecisionAction_Point"></a>

### Point


```csharp
public Point? Point { get; init; }
```


**Value:** `Point?`

<a id="E2E_Decision_DecisionAction_Target_"></a>
<a id="E2E_Decision_DecisionAction_Target"></a>

### Target


```csharp
public Target? Target { get; init; }
```


**Value:** [Target](/api/E2E.Decision/Target/)

<a id="E2E_Decision_DecisionAction_Text_"></a>
<a id="E2E_Decision_DecisionAction_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
