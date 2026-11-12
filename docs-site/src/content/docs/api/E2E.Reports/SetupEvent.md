---
title: "SetupEvent"
description: "Upstream's setup fact: the work before the tests start. Upstream has two members of one type, started and finished; here DurationMs and Outcome are set only..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SetupEvent"
  hidden: true
---

<a id="E2E_Reports_SetupEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record SetupEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<SetupEvent>
```


Upstream's `setup` fact: the work before the tests start. Upstream has two members of one type, `started` and `finished`; here
[DurationMs](#E2E_Reports_SetupEvent_DurationMs) and [Outcome](#E2E_Reports_SetupEvent_Outcome) are set only when [State](#E2E_Reports_SetupEvent_State) is [Finished](/api/E2E.Reports/SetupState/#E2E_Reports_SetupState_Finished).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;SetupEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_SetupEvent__ctor_"></a>
<a id="E2E_Reports_SetupEvent__ctor_E2E_Reports_SetupStep_E2E_Reports_SetupState_System_Int64_System_Nullable_E2E_Reports_SetupOutcome__"></a>

### SetupEvent(SetupStep, SetupState, long, SetupOutcome?)


```csharp
public SetupEvent(SetupStep Step, SetupState State, long DurationMs = 0, SetupOutcome? Outcome = null)
```


Upstream's `setup` fact: the work before the tests start. Upstream has two members of one type, `started` and `finished`; here
[DurationMs](#E2E_Reports_SetupEvent_DurationMs) and [Outcome](#E2E_Reports_SetupEvent_Outcome) are set only when [State](#E2E_Reports_SetupEvent_State) is [Finished](/api/E2E.Reports/SetupState/#E2E_Reports_SetupState_Finished).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Step` | [SetupStep](/api/E2E.Reports/SetupStep/) |  |
| `State` | [SetupState](/api/E2E.Reports/SetupState/) |  |
| `DurationMs` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |
| `Outcome` | `SetupOutcome?` |  |

## Properties

<a id="E2E_Reports_SetupEvent_DurationMs_"></a>
<a id="E2E_Reports_SetupEvent_DurationMs"></a>

### DurationMs


```csharp
public long DurationMs { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

<a id="E2E_Reports_SetupEvent_Outcome_"></a>
<a id="E2E_Reports_SetupEvent_Outcome"></a>

### Outcome


```csharp
public SetupOutcome? Outcome { get; init; }
```


**Value:** `SetupOutcome?`

<a id="E2E_Reports_SetupEvent_State_"></a>
<a id="E2E_Reports_SetupEvent_State"></a>

### State


```csharp
public SetupState State { get; init; }
```


**Value:** [SetupState](/api/E2E.Reports/SetupState/)

<a id="E2E_Reports_SetupEvent_Step_"></a>
<a id="E2E_Reports_SetupEvent_Step"></a>

### Step


```csharp
public SetupStep Step { get; init; }
```


**Value:** [SetupStep](/api/E2E.Reports/SetupStep/)
