---
title: "RunEventResult"
description: "Upstream's RunEventResult: a test's result with its target's identity. The attempts are the report's."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunEventResult"
  hidden: true
---

<a id="E2E_Reports_RunEventResult"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunEventResult : IEquatable<RunEventResult>
```


Upstream's `RunEventResult`: a test's result with its target's identity. The attempts are the report's.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RunEventResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Properties

<a id="E2E_Reports_RunEventResult_Agent_"></a>
<a id="E2E_Reports_RunEventResult_Agent"></a>

### Agent


```csharp
public string Agent { get; init; }
```


The configured agent the test ran as.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunEventResult_Attempts_"></a>
<a id="E2E_Reports_RunEventResult_Attempts"></a>

### Attempts


```csharp
public IReadOnlyList<ReportAttempt> Attempts { get; init; }
```


Every attempt the test has had so far, as the report tells them.

**Value:** [IReadOnlyList&lt;ReportAttempt&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Reports_RunEventResult_Repeat_"></a>
<a id="E2E_Reports_RunEventResult_Repeat"></a>

### Repeat


```csharp
public int Repeat { get; init; }
```


Which run of the test this is under repeat-each, 0 for the first.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_RunEventResult_ResultId_"></a>
<a id="E2E_Reports_RunEventResult_ResultId"></a>

### ResultId


```csharp
public required string ResultId { get; init; }
```


The report id of the result, which keys its trace page in [Traces](/api/E2E.Reports/RunFinishedEvent/#E2E_Reports_RunFinishedEvent_Traces).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunEventResult_Selected_"></a>
<a id="E2E_Reports_RunEventResult_Selected"></a>

### Selected


```csharp
public bool Selected { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Reports_RunEventResult_Skip_"></a>
<a id="E2E_Reports_RunEventResult_Skip"></a>

### Skip


```csharp
public SkipInfo? Skip { get; init; }
```


**Value:** [SkipInfo](/api/E2E.Reports/SkipInfo/)

<a id="E2E_Reports_RunEventResult_Status_"></a>
<a id="E2E_Reports_RunEventResult_Status"></a>

### Status


```csharp
public required ResultStatus Status { get; init; }
```


**Value:** [ResultStatus](/api/E2E.Reports/ResultStatus/)

<a id="E2E_Reports_RunEventResult_Target_"></a>
<a id="E2E_Reports_RunEventResult_Target"></a>

### Target


```csharp
public required (string Name, string Platform) Target { get; init; }
```


The target the result ran on, by its stable identity: upstream's inline `{ name, platform }`.

**Value:** [(string Name, string Platform)](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunEventResult_Test_"></a>
<a id="E2E_Reports_RunEventResult_Test"></a>

### Test


```csharp
public required TestIdentity Test { get; init; }
```


**Value:** [TestIdentity](/api/E2E.Reports/TestIdentity/)
