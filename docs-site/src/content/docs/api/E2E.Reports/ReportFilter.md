---
title: "ReportFilter"
description: "One test filter the run applied, in order (upstream's ReportFilter)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportFilter"
  hidden: true
---

<a id="E2E_Reports_ReportFilter"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportFilter : IEquatable<ReportFilter>
```


One test filter the run applied, in order (upstream's `ReportFilter`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportFilter&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L931)

## Properties

<a id="E2E_Reports_ReportFilter_Candidates_"></a>
<a id="E2E_Reports_ReportFilter_Candidates"></a>

### Candidates


```csharp
public int Candidates { get; init; }
```


How many tests it was given.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L937)

<a id="E2E_Reports_ReportFilter_Excluded_"></a>
<a id="E2E_Reports_ReportFilter_Excluded"></a>

### Excluded


```csharp
public int Excluded { get; init; }
```


How many tests the run left out on its word.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L940)

<a id="E2E_Reports_ReportFilter_Name_"></a>
<a id="E2E_Reports_ReportFilter_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


The filter's name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L934)

<a id="E2E_Reports_ReportFilter_Note_"></a>
<a id="E2E_Reports_ReportFilter_Note"></a>

### Note


```csharp
public string? Note { get; init; }
```


The filter's note, redacted; for a filter that failed or timed out, the warning saying why.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L943)

<a id="E2E_Reports_ReportFilter_Outcome_"></a>
<a id="E2E_Reports_ReportFilter_Outcome"></a>

### Outcome


```csharp
public FilterOutcome? Outcome { get; init; }
```


Set when the filter left nothing out because the call did not answer; null when it answered.

**Value:** `FilterOutcome?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L946)
