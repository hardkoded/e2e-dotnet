---
title: "ReportSummary"
description: "The run's counts by result (upstream's ReportSummary). Passed, Failed (timed out included), Interrupted, Flaky, and Skipped count selected results only and a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportSummary"
  hidden: true
---

<a id="E2E_Reports_ReportSummary"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportSummary : IEquatable<ReportSummary>
```


The run's counts by result (upstream's `ReportSummary`). [Passed](#E2E_Reports_ReportSummary_Passed), [Failed](#E2E_Reports_ReportSummary_Failed) (timed out included),
[Interrupted](#E2E_Reports_ReportSummary_Interrupted), [Flaky](#E2E_Reports_ReportSummary_Flaky), and [Skipped](#E2E_Reports_ReportSummary_Skipped) count selected results only and add up to [Selected](#E2E_Reports_ReportSummary_Selected).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportSummary&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L221)

## Properties

<a id="E2E_Reports_ReportSummary_Discovered_"></a>
<a id="E2E_Reports_ReportSummary_Discovered"></a>

### Discovered


```csharp
public int Discovered { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L223)

<a id="E2E_Reports_ReportSummary_Executed_"></a>
<a id="E2E_Reports_ReportSummary_Executed"></a>

### Executed


```csharp
public int Executed { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L227)

<a id="E2E_Reports_ReportSummary_Failed_"></a>
<a id="E2E_Reports_ReportSummary_Failed"></a>

### Failed


```csharp
public int Failed { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L231)

<a id="E2E_Reports_ReportSummary_Flaky_"></a>
<a id="E2E_Reports_ReportSummary_Flaky"></a>

### Flaky


```csharp
public int Flaky { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L236)

<a id="E2E_Reports_ReportSummary_Interrupted_"></a>
<a id="E2E_Reports_ReportSummary_Interrupted"></a>

### Interrupted


```csharp
public int Interrupted { get; init; }
```


Stopped by the run before reaching a verdict; never counted as failed.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L234)

<a id="E2E_Reports_ReportSummary_Passed_"></a>
<a id="E2E_Reports_ReportSummary_Passed"></a>

### Passed


```csharp
public int Passed { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L229)

<a id="E2E_Reports_ReportSummary_Selected_"></a>
<a id="E2E_Reports_ReportSummary_Selected"></a>

### Selected


```csharp
public int Selected { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L225)

<a id="E2E_Reports_ReportSummary_Skipped_"></a>
<a id="E2E_Reports_ReportSummary_Skipped"></a>

### Skipped


```csharp
public int Skipped { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L238)
