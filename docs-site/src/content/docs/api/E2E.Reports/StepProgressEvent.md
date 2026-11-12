---
title: "StepProgressEvent"
description: "Upstream's step fact: one moment of a running test's step, routed to its pair by test id, agent, target, and repeat. .NET-only name: StepEvent is the report'..."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepProgressEvent"
  hidden: true
---

<a id="E2E_Reports_StepProgressEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepProgressEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<StepProgressEvent>
```


Upstream's `step` fact: one moment of a running test's step, routed to its pair by test id, agent, target, and repeat. .NET-only name:
`StepEvent` is the report's model-turn record.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;StepProgressEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_StepProgressEvent__ctor_"></a>
<a id="E2E_Reports_StepProgressEvent__ctor_System_String_System_String_System_Int32_System_String_E2E_Reports_StepProgress_"></a>

### StepProgressEvent(string, string, int, string, StepProgress)


```csharp
public StepProgressEvent(string TestId, string Agent, int Repeat, string Target, StepProgress Progress)
```


Upstream's `step` fact: one moment of a running test's step, routed to its pair by test id, agent, target, and repeat. .NET-only name:
`StepEvent` is the report's model-turn record.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `TestId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Agent` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Repeat` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Progress` | [StepProgress](/api/E2E.Reports/StepProgress/) |  |

## Properties

<a id="E2E_Reports_StepProgressEvent_Agent_"></a>
<a id="E2E_Reports_StepProgressEvent_Agent"></a>

### Agent


```csharp
public string Agent { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepProgressEvent_Progress_"></a>
<a id="E2E_Reports_StepProgressEvent_Progress"></a>

### Progress


```csharp
public StepProgress Progress { get; init; }
```


**Value:** [StepProgress](/api/E2E.Reports/StepProgress/)

<a id="E2E_Reports_StepProgressEvent_Repeat_"></a>
<a id="E2E_Reports_StepProgressEvent_Repeat"></a>

### Repeat


```csharp
public int Repeat { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_StepProgressEvent_Target_"></a>
<a id="E2E_Reports_StepProgressEvent_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepProgressEvent_TestId_"></a>
<a id="E2E_Reports_StepProgressEvent_TestId"></a>

### TestId


```csharp
public string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
