---
title: "ReportChanges"
description: "The change a run's test filters judged, by counts only, or why no filter ran (upstream's ReportChanges): Base and Files once git measured the change, Skipped..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportChanges"
  hidden: true
---

<a id="E2E_Reports_ReportChanges"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportChanges : IEquatable<ReportChanges>
```


The change a run's test filters judged, by counts only, or why no filter ran (upstream's `ReportChanges`): `Base` and
`Files` once git measured the change, `Skipped` alone when no filter ran before git was asked.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportChanges&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L908)

## Properties

<a id="E2E_Reports_ReportChanges_Base_"></a>
<a id="E2E_Reports_ReportChanges_Base"></a>

### Base


```csharp
public string? Base { get; init; }
```


The merge base the change is measured from.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L911)

<a id="E2E_Reports_ReportChanges_Files_"></a>
<a id="E2E_Reports_ReportChanges_Files"></a>

### Files


```csharp
public int? Files { get; init; }
```


How many files changed since [Base](#E2E_Reports_ReportChanges_Base); 0 when there is no change.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L914)

<a id="E2E_Reports_ReportChanges_Skipped_"></a>
<a id="E2E_Reports_ReportChanges_Skipped"></a>

### Skipped


```csharp
public string? Skipped { get; init; }
```


Why no filter ran, such as `no files changed since abc1234`, `--last-failed`, or `no tests to judge`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L917)
