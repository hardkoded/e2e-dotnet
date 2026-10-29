---
title: "ReportExploreStep"
description: "One exploration step: a charter the agent planned and then executed as an ActAsync step."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExploreStep"
  hidden: true
---

<a id="E2E_ReportExploreStep"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ReportExploreStep : IEquatable<ReportExploreStep>
```


One exploration step: a charter the agent planned and then executed as an `ActAsync` step.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportExploreStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L83)

## Constructors

<a id="E2E_ReportExploreStep__ctor_"></a>
<a id="E2E_ReportExploreStep__ctor_System_Int32_System_String_System_String_E2E_ReportExploreStepStatus_System_DateTimeOffset_System_TimeSpan_System_String_System_String_"></a>

### ReportExploreStep(int, string, string, ReportExploreStepStatus, DateTimeOffset, TimeSpan, string?, string?)


```csharp
public ReportExploreStep(int Index, string Title, string Instruction, ReportExploreStepStatus Status, DateTimeOffset StartedAt, TimeSpan Duration, string? Summary = null, string? ErrorCode = null)
```


One exploration step: a charter the agent planned and then executed as an `ActAsync` step.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | One-based position of the step. |
| `Title` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The area or flow, at most 200 characters. |
| `Instruction` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The charter the agent ran, at most 2000 characters. |
| `Status` | [ReportExploreStepStatus](/api/E2E/ReportExploreStepStatus/) | How the step ended. |
| `StartedAt` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) | When the step began. |
| `Duration` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) | How long the step ran. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the step covered, at most 4000 characters. |
| `ErrorCode` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The code of the error that ended the step, when one did. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L83)

## Properties

<a id="E2E_ReportExploreStep_Duration_"></a>
<a id="E2E_ReportExploreStep_Duration"></a>

### Duration


```csharp
public TimeSpan Duration { get; init; }
```


How long the step ran.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L89)

<a id="E2E_ReportExploreStep_ErrorCode_"></a>
<a id="E2E_ReportExploreStep_ErrorCode"></a>

### ErrorCode


```csharp
public string? ErrorCode { get; init; }
```


The code of the error that ended the step, when one did.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L91)

<a id="E2E_ReportExploreStep_Index_"></a>
<a id="E2E_ReportExploreStep_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


One-based position of the step.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L84)

<a id="E2E_ReportExploreStep_Instruction_"></a>
<a id="E2E_ReportExploreStep_Instruction"></a>

### Instruction


```csharp
public string Instruction { get; init; }
```


The charter the agent ran, at most 2000 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L86)

<a id="E2E_ReportExploreStep_StartedAt_"></a>
<a id="E2E_ReportExploreStep_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset StartedAt { get; init; }
```


When the step began.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L88)

<a id="E2E_ReportExploreStep_Status_"></a>
<a id="E2E_ReportExploreStep_Status"></a>

### Status


```csharp
public ReportExploreStepStatus Status { get; init; }
```


How the step ended.

**Value:** [ReportExploreStepStatus](/api/E2E/ReportExploreStepStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L87)

<a id="E2E_ReportExploreStep_Summary_"></a>
<a id="E2E_ReportExploreStep_Summary"></a>

### Summary


```csharp
public string? Summary { get; init; }
```


What the step covered, at most 4000 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L90)

<a id="E2E_ReportExploreStep_Title_"></a>
<a id="E2E_ReportExploreStep_Title"></a>

### Title


```csharp
public string Title { get; init; }
```


The area or flow, at most 200 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L85)
