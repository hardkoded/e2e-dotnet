---
title: "ExploreEvent"
description: "Upstream's explore fact: one moment of an e2e explore run, the exploration starting as its test begins, the step in progress, each finding the moment it is r..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreEvent"
  hidden: true
---

<a id="E2E_Reports_ExploreEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ExploreEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<ExploreEvent>
```


Upstream's `explore` fact: one moment of an `e2e explore` run, the exploration starting as its test begins, the step in progress, each
finding the moment it is reported, and the end. The record is `run.explore` in the report.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_ExploreEvent__ctor_"></a>
<a id="E2E_Reports_ExploreEvent__ctor_E2E_ExploreProgress_"></a>

### ExploreEvent(ExploreProgress)


```csharp
public ExploreEvent(ExploreProgress Progress)
```


Upstream's `explore` fact: one moment of an `e2e explore` run, the exploration starting as its test begins, the step in progress, each
finding the moment it is reported, and the end. The record is `run.explore` in the report.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Progress` | [ExploreProgress](/api/E2E/ExploreProgress/) |  |

## Properties

<a id="E2E_Reports_ExploreEvent_Progress_"></a>
<a id="E2E_Reports_ExploreEvent_Progress"></a>

### Progress


```csharp
public ExploreProgress Progress { get; init; }
```


**Value:** [ExploreProgress](/api/E2E/ExploreProgress/)
