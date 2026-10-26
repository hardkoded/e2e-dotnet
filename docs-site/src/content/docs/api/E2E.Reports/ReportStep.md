---
title: "ReportStep"
description: "One call of the test (upstream's ReportStep)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportStep"
  hidden: true
---

<a id="E2E_Reports_ReportStep"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportStep : IEquatable<ReportStep>
```


One call of the test (upstream's `ReportStep`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L551)

## Properties

<a id="E2E_Reports_ReportStep_Agent_"></a>
<a id="E2E_Reports_ReportStep_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The configured agent an agent step ran with, by name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L599)

<a id="E2E_Reports_ReportStep_Api_"></a>
<a id="E2E_Reports_ReportStep_Api"></a>

### Api


```csharp
public string Api { get; init; }
```


The call, such as `locator.click`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L560)

<a id="E2E_Reports_ReportStep_Artifacts_"></a>
<a id="E2E_Reports_ReportStep_Artifacts"></a>

### Artifacts


```csharp
public IReadOnlyList<string> Artifacts { get; init; }
```


The ids of the artifacts the step made.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L609)

<a id="E2E_Reports_ReportStep_Cache_"></a>
<a id="E2E_Reports_ReportStep_Cache"></a>

### Cache


```csharp
public StepCacheRecord? Cache { get; init; }
```


**Value:** [StepCacheRecord](/api/E2E.Reports/StepCacheRecord/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L589)

<a id="E2E_Reports_ReportStep_DurationMs_"></a>
<a id="E2E_Reports_ReportStep_DurationMs"></a>

### DurationMs


```csharp
public long DurationMs { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L570)

<a id="E2E_Reports_ReportStep_Error_"></a>
<a id="E2E_Reports_ReportStep_Error"></a>

### Error


```csharp
public ReportError? Error { get; init; }
```


**Value:** [ReportError](/api/E2E.Reports/ReportError/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L606)

<a id="E2E_Reports_ReportStep_Events_"></a>
<a id="E2E_Reports_ReportStep_Events"></a>

### Events


```csharp
public IReadOnlyList<StepEvent> Events { get; init; }
```


**Value:** [IReadOnlyList&lt;StepEvent&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L591)

<a id="E2E_Reports_ReportStep_Explanation_"></a>
<a id="E2E_Reports_ReportStep_Explanation"></a>

### Explanation


```csharp
public string? Explanation { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L577)

<a id="E2E_Reports_ReportStep_Id_"></a>
<a id="E2E_Reports_ReportStep_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L553)

<a id="E2E_Reports_ReportStep_Index_"></a>
<a id="E2E_Reports_ReportStep_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L555)

<a id="E2E_Reports_ReportStep_Kind_"></a>
<a id="E2E_Reports_ReportStep_Kind"></a>

### Kind


```csharp
public StepKind Kind { get; init; }
```


**Value:** [StepKind](/api/E2E.Reports/StepKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L557)

<a id="E2E_Reports_ReportStep_Label_"></a>
<a id="E2E_Reports_ReportStep_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L562)

<a id="E2E_Reports_ReportStep_Metrics_"></a>
<a id="E2E_Reports_ReportStep_Metrics"></a>

### Metrics


```csharp
public StepMetrics? Metrics { get; init; }
```


**Value:** [StepMetrics](/api/E2E.Reports/StepMetrics/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L587)

<a id="E2E_Reports_ReportStep_Model_"></a>
<a id="E2E_Reports_ReportStep_Model"></a>

### Model


```csharp
public StepModelInfo? Model { get; init; }
```


**Value:** [StepModelInfo](/api/E2E.Reports/StepModelInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L596)

<a id="E2E_Reports_ReportStep_ObservationRevision_"></a>
<a id="E2E_Reports_ReportStep_ObservationRevision"></a>

### ObservationRevision


```csharp
public string? ObservationRevision { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L572)

<a id="E2E_Reports_ReportStep_ObservationTruncated_"></a>
<a id="E2E_Reports_ReportStep_ObservationTruncated"></a>

### ObservationTruncated


```csharp
public bool? ObservationTruncated { get; init; }
```


True when at least one semantic observation in the step was truncated (serialized `observationTruncated`).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L575)

<a id="E2E_Reports_ReportStep_Phase_"></a>
<a id="E2E_Reports_ReportStep_Phase"></a>

### Phase


```csharp
public StepPhase? Phase { get; init; }
```


**Value:** `StepPhase?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L601)

<a id="E2E_Reports_ReportStep_Screen_"></a>
<a id="E2E_Reports_ReportStep_Screen"></a>

### Screen


```csharp
public StepScreen? Screen { get; init; }
```


How the last screen the step saw differs from the step before's. Null when it saw none.

**Value:** [StepScreen](/api/E2E.Reports/StepScreen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L604)

<a id="E2E_Reports_ReportStep_Source_"></a>
<a id="E2E_Reports_ReportStep_Source"></a>

### Source


```csharp
public ReportSource Source { get; init; }
```


**Value:** [ReportSource](/api/E2E.Reports/ReportSource/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L564)

<a id="E2E_Reports_ReportStep_StartedAt_"></a>
<a id="E2E_Reports_ReportStep_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset StartedAt { get; init; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L568)

<a id="E2E_Reports_ReportStep_Status_"></a>
<a id="E2E_Reports_ReportStep_Status"></a>

### Status


```csharp
public StepStatus Status { get; init; }
```


**Value:** [StepStatus](/api/E2E.Reports/StepStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L566)

<a id="E2E_Reports_ReportStep_Turns_"></a>
<a id="E2E_Reports_ReportStep_Turns"></a>

### Turns


```csharp
public IReadOnlyList<StepTurn>? Turns { get; init; }
```


The last model turns of an agent step, oldest first.

**Value:** [IReadOnlyList&lt;StepTurn&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L594)

<a id="E2E_Reports_ReportStep_Viewport_"></a>
<a id="E2E_Reports_ReportStep_Viewport"></a>

### Viewport


```csharp
public StepViewport? Viewport { get; init; }
```


**Value:** [StepViewport](/api/E2E.Reports/StepViewport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L585)

<a id="E2E_Reports_ReportStep_VisionDegraded_"></a>
<a id="E2E_Reports_ReportStep_VisionDegraded"></a>

### VisionDegraded


```csharp
public VisionDegradation? VisionDegraded { get; init; }
```


**Value:** `VisionDegradation?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L581)

<a id="E2E_Reports_ReportStep_VisionInput_"></a>
<a id="E2E_Reports_ReportStep_VisionInput"></a>

### VisionInput


```csharp
public bool? VisionInput { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L579)

<a id="E2E_Reports_ReportStep_VisionOnly_"></a>
<a id="E2E_Reports_ReportStep_VisionOnly"></a>

### VisionOnly


```csharp
public bool? VisionOnly { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L583)
