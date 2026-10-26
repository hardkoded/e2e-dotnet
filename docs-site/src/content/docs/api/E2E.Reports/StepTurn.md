---
title: "StepTurn"
description: "One turn of an agent step (upstream's StepTurn)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepTurn"
  hidden: true
---

<a id="E2E_Reports_StepTurn"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepTurn : IEquatable<StepTurn>
```


One turn of an agent step (upstream's `StepTurn`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepTurn&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L775)

## Properties

<a id="E2E_Reports_StepTurn_Calls_"></a>
<a id="E2E_Reports_StepTurn_Calls"></a>

### Calls


```csharp
public IReadOnlyList<string> Calls { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L779)

<a id="E2E_Reports_StepTurn_Index_"></a>
<a id="E2E_Reports_StepTurn_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L777)

<a id="E2E_Reports_StepTurn_Outcome_"></a>
<a id="E2E_Reports_StepTurn_Outcome"></a>

### Outcome


```csharp
public string Outcome { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L781)
