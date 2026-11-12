---
title: "RunInterruptedEvent"
description: "Upstream's run-interrupted fact."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunInterruptedEvent"
  hidden: true
---

<a id="E2E_Reports_RunInterruptedEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunInterruptedEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<RunInterruptedEvent>
```


Upstream's `run-interrupted` fact.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RunInterruptedEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_RunInterruptedEvent__ctor_"></a>
<a id="E2E_Reports_RunInterruptedEvent__ctor_E2E_Reports_InterruptMode_"></a>

### RunInterruptedEvent(InterruptMode)


```csharp
public RunInterruptedEvent(InterruptMode Mode)
```


Upstream's `run-interrupted` fact.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Mode` | [InterruptMode](/api/E2E.Reports/InterruptMode/) |  |

## Properties

<a id="E2E_Reports_RunInterruptedEvent_Mode_"></a>
<a id="E2E_Reports_RunInterruptedEvent_Mode"></a>

### Mode


```csharp
public InterruptMode Mode { get; init; }
```


**Value:** [InterruptMode](/api/E2E.Reports/InterruptMode/)
