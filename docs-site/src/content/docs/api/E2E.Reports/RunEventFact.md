---
title: "RunEventFact"
description: "One fact about the run; the emitter wraps it in the RunEvent envelope. One record per upstream event type."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunEventFact"
  hidden: true
---

<a id="E2E_Reports_RunEventFact"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public abstract record RunEventFact : IEquatable<RunEventFact>
```


One fact about the run; the emitter wraps it in the [RunEvent](/api/E2E.Reports/RunEvent/) envelope. One record per upstream event type.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [ExploreEvent](/api/E2E.Reports/ExploreEvent/), [NoticeEvent](/api/E2E.Reports/NoticeEvent/), [OutputEvent](/api/E2E.Reports/OutputEvent/), [PlanEvent](/api/E2E.Reports/PlanEvent/), [RunErrorEvent](/api/E2E.Reports/RunErrorEvent/), [RunFinishedEvent](/api/E2E.Reports/RunFinishedEvent/), [RunInterruptedEvent](/api/E2E.Reports/RunInterruptedEvent/), [RunStartedEvent](/api/E2E.Reports/RunStartedEvent/), [RunStoppedEvent](/api/E2E.Reports/RunStoppedEvent/), [SetupEvent](/api/E2E.Reports/SetupEvent/), [StepProgressEvent](/api/E2E.Reports/StepProgressEvent/), [TestFinishedEvent](/api/E2E.Reports/TestFinishedEvent/), [TestStartedEvent](/api/E2E.Reports/TestStartedEvent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)
