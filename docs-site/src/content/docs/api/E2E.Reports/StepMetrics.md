---
title: "StepMetrics"
description: "What an agent step used (upstream's StepMetrics)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepMetrics"
  hidden: true
---

<a id="E2E_Reports_StepMetrics"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepMetrics : IEquatable<StepMetrics>
```


What an agent step used (upstream's `StepMetrics`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepMetrics&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L623)

## Properties

<a id="E2E_Reports_StepMetrics_ActionSteps_"></a>
<a id="E2E_Reports_StepMetrics_ActionSteps"></a>

### ActionSteps


```csharp
public int ActionSteps { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L627)

<a id="E2E_Reports_StepMetrics_ContextBytes_"></a>
<a id="E2E_Reports_StepMetrics_ContextBytes"></a>

### ContextBytes


```csharp
public int ContextBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L631)

<a id="E2E_Reports_StepMetrics_LedgerBytes_"></a>
<a id="E2E_Reports_StepMetrics_LedgerBytes"></a>

### LedgerBytes


```csharp
public int LedgerBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L633)

<a id="E2E_Reports_StepMetrics_ModelCalls_"></a>
<a id="E2E_Reports_StepMetrics_ModelCalls"></a>

### ModelCalls


```csharp
public int ModelCalls { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L625)

<a id="E2E_Reports_StepMetrics_ObservationBytes_"></a>
<a id="E2E_Reports_StepMetrics_ObservationBytes"></a>

### ObservationBytes


```csharp
public int ObservationBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L629)

<a id="E2E_Reports_StepMetrics_PixelBytes_"></a>
<a id="E2E_Reports_StepMetrics_PixelBytes"></a>

### PixelBytes


```csharp
public int? PixelBytes { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L635)
