---
title: "TestFinishedEvent"
description: "Upstream's test-finished fact."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestFinishedEvent"
  hidden: true
---

<a id="E2E_Reports_TestFinishedEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record TestFinishedEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<TestFinishedEvent>
```


Upstream's `test-finished` fact.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;TestFinishedEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_TestFinishedEvent__ctor_"></a>
<a id="E2E_Reports_TestFinishedEvent__ctor_E2E_Reports_RunEventResult_"></a>

### TestFinishedEvent(RunEventResult)


```csharp
public TestFinishedEvent(RunEventResult Result)
```


Upstream's `test-finished` fact.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Result` | [RunEventResult](/api/E2E.Reports/RunEventResult/) |  |

## Properties

<a id="E2E_Reports_TestFinishedEvent_Result_"></a>
<a id="E2E_Reports_TestFinishedEvent_Result"></a>

### Result


```csharp
public RunEventResult Result { get; init; }
```


**Value:** [RunEventResult](/api/E2E.Reports/RunEventResult/)
