---
title: "ReportResult"
description: "One test over all its attempts (upstream's ReportResult)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportResult"
  hidden: true
---

<a id="E2E_Reports_ReportResult"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportResult : IEquatable<ReportResult>
```


One test over all its attempts (upstream's `ReportResult`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L260)

## Properties

<a id="E2E_Reports_ReportResult_Agent_"></a>
<a id="E2E_Reports_ReportResult_Agent"></a>

### Agent


```csharp
public string Agent { get; init; }
```


The configured agent the test ran as.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L283)

<a id="E2E_Reports_ReportResult_Attempts_"></a>
<a id="E2E_Reports_ReportResult_Attempts"></a>

### Attempts


```csharp
public IReadOnlyList<ReportAttempt> Attempts { get; init; }
```


**Value:** [IReadOnlyList&lt;ReportAttempt&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L298)

<a id="E2E_Reports_ReportResult_DeclarationIndex_"></a>
<a id="E2E_Reports_ReportResult_DeclarationIndex"></a>

### DeclarationIndex


```csharp
public int DeclarationIndex { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L268)

<a id="E2E_Reports_ReportResult_File_"></a>
<a id="E2E_Reports_ReportResult_File"></a>

### File


```csharp
public string File { get; init; }
```


The test file, relative to the project root.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L273)

<a id="E2E_Reports_ReportResult_Id_"></a>
<a id="E2E_Reports_ReportResult_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L262)

<a id="E2E_Reports_ReportResult_Kind_"></a>
<a id="E2E_Reports_ReportResult_Kind"></a>

### Kind


```csharp
public ResultKind Kind { get; init; }
```


**Value:** [ResultKind](/api/E2E.Reports/ResultKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L266)

<a id="E2E_Reports_ReportResult_Platform_"></a>
<a id="E2E_Reports_ReportResult_Platform"></a>

### Platform


```csharp
public string Platform { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L280)

<a id="E2E_Reports_ReportResult_Repeat_"></a>
<a id="E2E_Reports_ReportResult_Repeat"></a>

### Repeat


```csharp
public int Repeat { get; init; }
```


Which run of the test this is under repeated runs: 0 for the first.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L286)

<a id="E2E_Reports_ReportResult_Selected_"></a>
<a id="E2E_Reports_ReportResult_Selected"></a>

### Selected


```csharp
public bool Selected { get; init; }
```


Whether the selection chose this test. A test left out is still reported, as skipped, but the summary's `Selected` does not count it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L292)

<a id="E2E_Reports_ReportResult_Skip_"></a>
<a id="E2E_Reports_ReportResult_Skip"></a>

### Skip


```csharp
public SkipInfo? Skip { get; init; }
```


**Value:** [SkipInfo](/api/E2E.Reports/SkipInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L296)

<a id="E2E_Reports_ReportResult_Source_"></a>
<a id="E2E_Reports_ReportResult_Source"></a>

### Source


```csharp
public ReportSource Source { get; init; }
```


Where the test is declared.

**Value:** [ReportSource](/api/E2E.Reports/ReportSource/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L276)

<a id="E2E_Reports_ReportResult_Status_"></a>
<a id="E2E_Reports_ReportResult_Status"></a>

### Status


```csharp
public ResultStatus Status { get; init; }
```


**Value:** [ResultStatus](/api/E2E.Reports/ResultStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L294)

<a id="E2E_Reports_ReportResult_Tags_"></a>
<a id="E2E_Reports_ReportResult_Tags"></a>

### Tags


```csharp
public IReadOnlyList<string> Tags { get; init; }
```


The tags the test declares; empty when none.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L289)

<a id="E2E_Reports_ReportResult_TargetId_"></a>
<a id="E2E_Reports_ReportResult_TargetId"></a>

### TargetId


```csharp
public string TargetId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L278)

<a id="E2E_Reports_ReportResult_TestId_"></a>
<a id="E2E_Reports_ReportResult_TestId"></a>

### TestId


```csharp
public string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L264)

<a id="E2E_Reports_ReportResult_TitlePath_"></a>
<a id="E2E_Reports_ReportResult_TitlePath"></a>

### TitlePath


```csharp
public IReadOnlyList<string> TitlePath { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L270)
