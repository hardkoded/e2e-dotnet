---
title: "RunStoppedEvent"
description: "Upstream's run-stopped fact: the run reached its failure limit."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunStoppedEvent"
  hidden: true
---

<a id="E2E_Reports_RunStoppedEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunStoppedEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<RunStoppedEvent>
```


Upstream's `run-stopped` fact: the run reached its failure limit.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RunStoppedEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_RunStoppedEvent__ctor_"></a>
<a id="E2E_Reports_RunStoppedEvent__ctor_System_Int32_System_Int32_"></a>

### RunStoppedEvent(int, int)


```csharp
public RunStoppedEvent(int Failures, int Limit)
```


Upstream's `run-stopped` fact: the run reached its failure limit.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Failures` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Limit` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Reports_RunStoppedEvent_Failures_"></a>
<a id="E2E_Reports_RunStoppedEvent_Failures"></a>

### Failures


```csharp
public int Failures { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_RunStoppedEvent_Limit_"></a>
<a id="E2E_Reports_RunStoppedEvent_Limit"></a>

### Limit


```csharp
public int Limit { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
