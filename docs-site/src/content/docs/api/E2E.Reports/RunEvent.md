---
title: "RunEvent"
description: "Structured run events: the run's single event spine (upstream's run/events.ts), as a Reporter sees it through OnEvent(RunEvent). The list reporter consumes t..."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunEvent"
  hidden: true
---

<a id="E2E_Reports_RunEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunEvent : IEquatable<RunEvent>
```


Structured run events: the run's single event spine (upstream's `run/events.ts`), as a [Reporter](/api/E2E.Reports/Reporter/) sees it through
[OnEvent(RunEvent)](/api/E2E.Reports/Reporter/#E2E_Reports_Reporter_OnEvent_E2E_Reports_RunEvent_). The list reporter consumes the same stream (`ListReporter.Handle`), so no two consumers can drift. The
port's test run fixtures and sessions emit them: the run's start, plan, notices, setup steps, started tests, step progress, finished tests, errors,
interrupts, and end. Not ported: `output` (the test framework captures stdout), `serial-group` (the port has no serial groups), and
`explore`, which feeds the live window the port has no use for yet. A new event type may be added, so a reporter handles the ones it
knows and ignores the rest.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RunEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_RunEvent__ctor_"></a>
<a id="E2E_Reports_RunEvent__ctor_System_Int64_System_DateTimeOffset_E2E_Reports_RunEventFact_"></a>

### RunEvent(long, DateTimeOffset, RunEventFact)


```csharp
public RunEvent(long Seq, DateTimeOffset At, RunEventFact Fact)
```


Structured run events: the run's single event spine (upstream's `run/events.ts`), as a [Reporter](/api/E2E.Reports/Reporter/) sees it through
[OnEvent(RunEvent)](/api/E2E.Reports/Reporter/#E2E_Reports_Reporter_OnEvent_E2E_Reports_RunEvent_). The list reporter consumes the same stream (`ListReporter.Handle`), so no two consumers can drift. The
port's test run fixtures and sessions emit them: the run's start, plan, notices, setup steps, started tests, step progress, finished tests, errors,
interrupts, and end. Not ported: `output` (the test framework captures stdout), `serial-group` (the port has no serial groups), and
`explore`, which feeds the live window the port has no use for yet. A new event type may be added, so a reporter handles the ones it
knows and ignores the rest.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Seq` | [long](https://learn.microsoft.com/dotnet/api/system.int64) | The event's place in the run's order, from 1. Upstream's `seq`. |
| `At` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) | When the event was emitted. Upstream's `at`. |
| `Fact` | [RunEventFact](/api/E2E.Reports/RunEventFact/) | What happened. Upstream spreads the fact into the event; a C# record keeps it as a member. |

## Properties

<a id="E2E_Reports_RunEvent_At_"></a>
<a id="E2E_Reports_RunEvent_At"></a>

### At


```csharp
public DateTimeOffset At { get; init; }
```


When the event was emitted. Upstream's `at`.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Reports_RunEvent_Fact_"></a>
<a id="E2E_Reports_RunEvent_Fact"></a>

### Fact


```csharp
public RunEventFact Fact { get; init; }
```


What happened. Upstream spreads the fact into the event; a C# record keeps it as a member.

**Value:** [RunEventFact](/api/E2E.Reports/RunEventFact/)

<a id="E2E_Reports_RunEvent_Seq_"></a>
<a id="E2E_Reports_RunEvent_Seq"></a>

### Seq


```csharp
public long Seq { get; init; }
```


The event's place in the run's order, from 1. Upstream's `seq`.

**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)
