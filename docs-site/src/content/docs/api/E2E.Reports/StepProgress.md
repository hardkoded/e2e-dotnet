---
title: "StepProgress"
description: "Live progress notification for one step, streamed as the step runs (upstream's StepProgress, run/steps.ts). Derived from the same records the report persists..."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepProgress"
  hidden: true
---

<a id="E2E_Reports_StepProgress"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepProgress : IEquatable<StepProgress>
```


Live progress notification for one step, streamed as the step runs (upstream's `StepProgress`, `run/steps.ts`). Derived from the same
records the report persists; the report stays the canonical record. Which members are set depends on [Phase](#E2E_Reports_StepProgress_Phase). It lives in
`E2E.Reports` with the report types it uses, because the port's own records for the same parts keep upstream's names in `E2E`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L37)

## Properties

<a id="E2E_Reports_StepProgress_Active_"></a>
<a id="E2E_Reports_StepProgress_Active"></a>

### Active


```csharp
public bool Active { get; init; }
```


Replay: whether the trace cache has the step.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L73)

<a id="E2E_Reports_StepProgress_Activity_"></a>
<a id="E2E_Reports_StepProgress_Activity"></a>

### Activity


```csharp
public StepActivity? Activity { get; init; }
```


Activity.

**Value:** `StepActivity?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L70)

<a id="E2E_Reports_StepProgress_Api_"></a>
<a id="E2E_Reports_StepProgress_Api"></a>

### Api


```csharp
public string Api { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L43)

<a id="E2E_Reports_StepProgress_DurationMs_"></a>
<a id="E2E_Reports_StepProgress_DurationMs"></a>

### DurationMs


```csharp
public long DurationMs { get; init; }
```


End.

**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L55)

<a id="E2E_Reports_StepProgress_Error_"></a>
<a id="E2E_Reports_StepProgress_Error"></a>

### Error


```csharp
public ReportError? Error { get; init; }
```


End: the same redacted error the finished step records.

**Value:** [ReportError](/api/E2E.Reports/ReportError/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L61)

<a id="E2E_Reports_StepProgress_Event_"></a>
<a id="E2E_Reports_StepProgress_Event"></a>

### Event


```csharp
public StepEvent? Event { get; init; }
```


Event.

**Value:** [StepEvent](/api/E2E.Reports/StepEvent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L67)

<a id="E2E_Reports_StepProgress_Explanation_"></a>
<a id="E2E_Reports_StepProgress_Explanation"></a>

### Explanation


```csharp
public string? Explanation { get; init; }
```


End.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L64)

<a id="E2E_Reports_StepProgress_Identity_"></a>
<a id="E2E_Reports_StepProgress_Identity"></a>

### Identity


```csharp
public StepProgressIdentity? Identity { get; init; }
```


**Value:** [StepProgressIdentity](/api/E2E.Reports/StepProgressIdentity/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L41)

<a id="E2E_Reports_StepProgress_Kind_"></a>
<a id="E2E_Reports_StepProgress_Kind"></a>

### Kind


```csharp
public StepKind? Kind { get; init; }
```


Start and end.

**Value:** `StepKind?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L46)

<a id="E2E_Reports_StepProgress_Label_"></a>
<a id="E2E_Reports_StepProgress_Label"></a>

### Label


```csharp
public string? Label { get; init; }
```


Start and end.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L49)

<a id="E2E_Reports_StepProgress_ModelCalls_"></a>
<a id="E2E_Reports_StepProgress_ModelCalls"></a>

### ModelCalls


```csharp
public int ModelCalls { get; init; }
```


End.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L58)

<a id="E2E_Reports_StepProgress_Phase_"></a>
<a id="E2E_Reports_StepProgress_Phase"></a>

### Phase


```csharp
public StepProgressPhase Phase { get; init; }
```


**Value:** [StepProgressPhase](/api/E2E.Reports/StepProgressPhase/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L39)

<a id="E2E_Reports_StepProgress_Status_"></a>
<a id="E2E_Reports_StepProgress_Status"></a>

### Status


```csharp
public StepStatus? Status { get; init; }
```


End.

**Value:** `StepStatus?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L52)
