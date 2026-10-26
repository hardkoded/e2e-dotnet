---
title: "ReportRun"
description: "The run of a report: its id, the targets it ran on, the results, and the counts (upstream's Report1Document['run'])."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportRun"
  hidden: true
---

<a id="E2E_Reports_ReportRun"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportRun : IEquatable<ReportRun>
```


The run of a report: its id, the targets it ran on, the results, and the counts (upstream's `Report1Document['run']`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportRun&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L22)

## Properties

<a id="E2E_Reports_ReportRun_Changes_"></a>
<a id="E2E_Reports_ReportRun_Changes"></a>

### Changes


```csharp
public ReportChanges? Changes { get; init; }
```


The change the test filters judged, or why none ran. Null when the run has no filters to apply.

**Value:** [ReportChanges](/api/E2E.Reports/ReportChanges/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L61)

<a id="E2E_Reports_ReportRun_Environment_"></a>
<a id="E2E_Reports_ReportRun_Environment"></a>

### Environment


```csharp
public ReportEnvironment Environment { get; init; }
```


**Value:** [ReportEnvironment](/api/E2E.Reports/ReportEnvironment/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L42)

<a id="E2E_Reports_ReportRun_Errors_"></a>
<a id="E2E_Reports_ReportRun_Errors"></a>

### Errors


```csharp
public IReadOnlyList<ReportError> Errors { get; init; }
```


The errors of the run itself, such as a report that could not be written.

**Value:** [IReadOnlyList&lt;ReportError&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L52)

<a id="E2E_Reports_ReportRun_ExitCode_"></a>
<a id="E2E_Reports_ReportRun_ExitCode"></a>

### ExitCode


```csharp
public int ExitCode { get; init; }
```


0, 1, 2, 3, 4, or 130.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L34)

<a id="E2E_Reports_ReportRun_Explore_"></a>
<a id="E2E_Reports_ReportRun_Explore"></a>

### Explore


```csharp
public ReportExplore? Explore { get; init; }
```


What an exploration asked, did, and found. Null on a run that did not explore.

**Value:** [ReportExplore](/api/E2E/ReportExplore/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L67)

<a id="E2E_Reports_ReportRun_Filters_"></a>
<a id="E2E_Reports_ReportRun_Filters"></a>

### Filters


```csharp
public IReadOnlyList<ReportFilter>? Filters { get; init; }
```


The test filters the run applied, in order. Null when none ran.

**Value:** [IReadOnlyList&lt;ReportFilter&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L64)

<a id="E2E_Reports_ReportRun_FinishedAt_"></a>
<a id="E2E_Reports_ReportRun_FinishedAt"></a>

### FinishedAt


```csharp
public DateTimeOffset? FinishedAt { get; init; }
```


**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L38)

<a id="E2E_Reports_ReportRun_Id_"></a>
<a id="E2E_Reports_ReportRun_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L24)

<a id="E2E_Reports_ReportRun_Limits_"></a>
<a id="E2E_Reports_ReportRun_Limits"></a>

### Limits


```csharp
public ReportLimits Limits { get; init; }
```


**Value:** [ReportLimits](/api/E2E.Reports/ReportLimits/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L56)

<a id="E2E_Reports_ReportRun_Project_"></a>
<a id="E2E_Reports_ReportRun_Project"></a>

### Project


```csharp
public ReportProject Project { get; init; }
```


**Value:** [ReportProject](/api/E2E.Reports/ReportProject/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L40)

<a id="E2E_Reports_ReportRun_Results_"></a>
<a id="E2E_Reports_ReportRun_Results"></a>

### Results


```csharp
public IReadOnlyList<ReportResult> Results { get; init; }
```


**Value:** [IReadOnlyList&lt;ReportResult&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L49)

<a id="E2E_Reports_ReportRun_Runner_"></a>
<a id="E2E_Reports_ReportRun_Runner"></a>

### Runner


```csharp
public ReportRunner Runner { get; init; }
```


**Value:** [ReportRunner](/api/E2E.Reports/ReportRunner/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L29)

<a id="E2E_Reports_ReportRun_SpecVersion_"></a>
<a id="E2E_Reports_ReportRun_SpecVersion"></a>

### SpecVersion


```csharp
public string SpecVersion { get; init; }
```


Always `0.1`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L27)

<a id="E2E_Reports_ReportRun_StartedAt_"></a>
<a id="E2E_Reports_ReportRun_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset StartedAt { get; init; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L36)

<a id="E2E_Reports_ReportRun_Status_"></a>
<a id="E2E_Reports_ReportRun_Status"></a>

### Status


```csharp
public ReportRunStatus Status { get; init; }
```


**Value:** [ReportRunStatus](/api/E2E/ReportRunStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L31)

<a id="E2E_Reports_ReportRun_Summary_"></a>
<a id="E2E_Reports_ReportRun_Summary"></a>

### Summary


```csharp
public ReportSummary Summary { get; init; }
```


**Value:** [ReportSummary](/api/E2E.Reports/ReportSummary/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L54)

<a id="E2E_Reports_ReportRun_Targets_"></a>
<a id="E2E_Reports_ReportRun_Targets"></a>

### Targets


```csharp
public IReadOnlyList<ReportTarget> Targets { get; init; }
```


**Value:** [IReadOnlyList&lt;ReportTarget&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L47)

<a id="E2E_Reports_ReportRun_Usage_"></a>
<a id="E2E_Reports_ReportRun_Usage"></a>

### Usage


```csharp
public ReportUsage Usage { get; init; }
```


**Value:** [ReportUsage](/api/E2E.Reports/ReportUsage/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L58)

<a id="E2E_Reports_ReportRun_Vcs_"></a>
<a id="E2E_Reports_ReportRun_Vcs"></a>

### Vcs


```csharp
public VcsInfo? Vcs { get; init; }
```


The commit, branch, and cleanliness of the checkout the run tested. Null outside git and CI.

**Value:** [VcsInfo](/api/E2E.Reports/VcsInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L45)
