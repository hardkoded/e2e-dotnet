---
title: "ReportAttempt"
description: "One attempt of a test (upstream's ReportAttempt)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportAttempt"
  hidden: true
---

<a id="E2E_Reports_ReportAttempt"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportAttempt : IEquatable<ReportAttempt>
```


One attempt of a test (upstream's `ReportAttempt`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportAttempt&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L349)

## Properties

<a id="E2E_Reports_ReportAttempt_AppLog_"></a>
<a id="E2E_Reports_ReportAttempt_AppLog"></a>

### AppLog


```csharp
public IReadOnlyList<AppLogRecord>? AppLog { get; init; }
```


What the app logged during the attempt, each line with the index of its step. Null when it logged nothing.

**Value:** [IReadOnlyList&lt;AppLogRecord&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L366)

<a id="E2E_Reports_ReportAttempt_Artifacts_"></a>
<a id="E2E_Reports_ReportAttempt_Artifacts"></a>

### Artifacts


```csharp
public IReadOnlyList<ArtifactRecord> Artifacts { get; init; }
```


**Value:** [IReadOnlyList&lt;ArtifactRecord&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L368)

<a id="E2E_Reports_ReportAttempt_Cleanup_"></a>
<a id="E2E_Reports_ReportAttempt_Cleanup"></a>

### Cleanup


```csharp
public AttemptCleanup Cleanup { get; init; }
```


**Value:** [AttemptCleanup](/api/E2E.Reports/AttemptCleanup/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L382)

<a id="E2E_Reports_ReportAttempt_DurationMs_"></a>
<a id="E2E_Reports_ReportAttempt_DurationMs"></a>

### DurationMs


```csharp
public long DurationMs { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L361)

<a id="E2E_Reports_ReportAttempt_Environment_"></a>
<a id="E2E_Reports_ReportAttempt_Environment"></a>

### Environment


```csharp
public IReadOnlyDictionary<string, string>? Environment { get; init; }
```


What the engine said the attempt ran on, such as `browser` or `device`. Null when it said nothing.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L385)

<a id="E2E_Reports_ReportAttempt_Error_"></a>
<a id="E2E_Reports_ReportAttempt_Error"></a>

### Error


```csharp
public ReportError? Error { get; init; }
```


What stopped the attempt.

**Value:** [ReportError](/api/E2E.Reports/ReportError/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L371)

<a id="E2E_Reports_ReportAttempt_Failure_"></a>
<a id="E2E_Reports_ReportAttempt_Failure"></a>

### Failure


```csharp
public FailureEvidence? Failure { get; init; }
```


What the runner saw when the failure landed. Null on a pass or when nothing could be captured.

**Value:** [FailureEvidence](/api/E2E.Reports/FailureEvidence/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L374)

<a id="E2E_Reports_ReportAttempt_Id_"></a>
<a id="E2E_Reports_ReportAttempt_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


A UUID version 7; also the head of its steps' ids.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L352)

<a id="E2E_Reports_ReportAttempt_Index_"></a>
<a id="E2E_Reports_ReportAttempt_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


Zero-based.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L355)

<a id="E2E_Reports_ReportAttempt_SecondaryErrors_"></a>
<a id="E2E_Reports_ReportAttempt_SecondaryErrors"></a>

### SecondaryErrors


```csharp
public IReadOnlyList<ReportError> SecondaryErrors { get; init; }
```


What else failed after the error: a teardown hook, the engine's cleanup.

**Value:** [IReadOnlyList&lt;ReportError&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L380)

<a id="E2E_Reports_ReportAttempt_Skip_"></a>
<a id="E2E_Reports_ReportAttempt_Skip"></a>

### Skip


```csharp
public SkipInfo? Skip { get; init; }
```


Why the body skipped itself.

**Value:** [SkipInfo](/api/E2E.Reports/SkipInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L377)

<a id="E2E_Reports_ReportAttempt_StartedAt_"></a>
<a id="E2E_Reports_ReportAttempt_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset StartedAt { get; init; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L359)

<a id="E2E_Reports_ReportAttempt_Status_"></a>
<a id="E2E_Reports_ReportAttempt_Status"></a>

### Status


```csharp
public AttemptStatus Status { get; init; }
```


**Value:** [AttemptStatus](/api/E2E.Reports/AttemptStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L357)

<a id="E2E_Reports_ReportAttempt_Steps_"></a>
<a id="E2E_Reports_ReportAttempt_Steps"></a>

### Steps


```csharp
public IReadOnlyList<ReportStep> Steps { get; init; }
```


**Value:** [IReadOnlyList&lt;ReportStep&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L363)
