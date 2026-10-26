---
title: "StepEvent"
description: "One thing a step did (upstream's StepEvent)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepEvent"
  hidden: true
---

<a id="E2E_Reports_StepEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepEvent : IEquatable<StepEvent>
```


One thing a step did (upstream's `StepEvent`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L813)

## Properties

<a id="E2E_Reports_StepEvent_Bytes_"></a>
<a id="E2E_Reports_StepEvent_Bytes"></a>

### Bytes


```csharp
public long? Bytes { get; init; }
```


**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L831)

<a id="E2E_Reports_StepEvent_Code_"></a>
<a id="E2E_Reports_StepEvent_Code"></a>

### Code


```csharp
public string? Code { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L835)

<a id="E2E_Reports_StepEvent_Count_"></a>
<a id="E2E_Reports_StepEvent_Count"></a>

### Count


```csharp
public int? Count { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L825)

<a id="E2E_Reports_StepEvent_Decision_"></a>
<a id="E2E_Reports_StepEvent_Decision"></a>

### Decision


```csharp
public PolicyDecision? Decision { get; init; }
```


**Value:** `PolicyDecision?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L833)

<a id="E2E_Reports_StepEvent_Detail_"></a>
<a id="E2E_Reports_StepEvent_Detail"></a>

### Detail


```csharp
public string? Detail { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L837)

<a id="E2E_Reports_StepEvent_DurationMs_"></a>
<a id="E2E_Reports_StepEvent_DurationMs"></a>

### DurationMs


```csharp
public long DurationMs { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L819)

<a id="E2E_Reports_StepEvent_InputTokens_"></a>
<a id="E2E_Reports_StepEvent_InputTokens"></a>

### InputTokens


```csharp
public long? InputTokens { get; init; }
```


**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L827)

<a id="E2E_Reports_StepEvent_Kind_"></a>
<a id="E2E_Reports_StepEvent_Kind"></a>

### Kind


```csharp
public StepEventKind Kind { get; init; }
```


**Value:** [StepEventKind](/api/E2E.Reports/StepEventKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L815)

<a id="E2E_Reports_StepEvent_Name_"></a>
<a id="E2E_Reports_StepEvent_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L823)

<a id="E2E_Reports_StepEvent_OutputTokens_"></a>
<a id="E2E_Reports_StepEvent_OutputTokens"></a>

### OutputTokens


```csharp
public long? OutputTokens { get; init; }
```


**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L829)

<a id="E2E_Reports_StepEvent_Reasoning_"></a>
<a id="E2E_Reports_StepEvent_Reasoning"></a>

### Reasoning


```csharp
public string? Reasoning { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L839)

<a id="E2E_Reports_StepEvent_StartedAt_"></a>
<a id="E2E_Reports_StepEvent_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset StartedAt { get; init; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L817)

<a id="E2E_Reports_StepEvent_Status_"></a>
<a id="E2E_Reports_StepEvent_Status"></a>

### Status


```csharp
public StepEventStatus Status { get; init; }
```


**Value:** [StepEventStatus](/api/E2E.Reports/StepEventStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L821)
