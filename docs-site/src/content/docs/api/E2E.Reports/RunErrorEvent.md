---
title: "RunErrorEvent"
description: "Upstream's run-error fact: a run-level error, which aborts before, between, or after test execution."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunErrorEvent"
  hidden: true
---

<a id="E2E_Reports_RunErrorEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunErrorEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<RunErrorEvent>
```


Upstream's `run-error` fact: a run-level error, which aborts before, between, or after test execution.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RunErrorEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_RunErrorEvent__ctor_"></a>
<a id="E2E_Reports_RunErrorEvent__ctor_E2E_Reports_ReportError_"></a>

### RunErrorEvent(ReportError)


```csharp
public RunErrorEvent(ReportError Error)
```


Upstream's `run-error` fact: a run-level error, which aborts before, between, or after test execution.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Error` | [ReportError](/api/E2E.Reports/ReportError/) |  |

## Properties

<a id="E2E_Reports_RunErrorEvent_Error_"></a>
<a id="E2E_Reports_RunErrorEvent_Error"></a>

### Error


```csharp
public ReportError Error { get; init; }
```


**Value:** [ReportError](/api/E2E.Reports/ReportError/)
