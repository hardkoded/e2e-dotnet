---
title: "RunFinishedEvent"
description: "Upstream's run-finished fact."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunFinishedEvent"
  hidden: true
---

<a id="E2E_Reports_RunFinishedEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunFinishedEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<RunFinishedEvent>
```


Upstream's `run-finished` fact.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RunFinishedEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Properties

<a id="E2E_Reports_RunFinishedEvent_AiTracePath_"></a>
<a id="E2E_Reports_RunFinishedEvent_AiTracePath"></a>

### AiTracePath


```csharp
public string? AiTracePath { get; init; }
```


Where the run's model calls were written, once the file exists.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunFinishedEvent_ExitCode_"></a>
<a id="E2E_Reports_RunFinishedEvent_ExitCode"></a>

### ExitCode


```csharp
public int ExitCode { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_RunFinishedEvent_ReportPath_"></a>
<a id="E2E_Reports_RunFinishedEvent_ReportPath"></a>

### ReportPath


```csharp
public string? ReportPath { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunFinishedEvent_Status_"></a>
<a id="E2E_Reports_RunFinishedEvent_Status"></a>

### Status


```csharp
public required ReportRunStatus Status { get; init; }
```


**Value:** [ReportRunStatus](/api/E2E/ReportRunStatus/)

<a id="E2E_Reports_RunFinishedEvent_Traces_"></a>
<a id="E2E_Reports_RunFinishedEvent_Traces"></a>

### Traces


```csharp
public IReadOnlyDictionary<string, string>? Traces { get; init; }
```


The trace page written for each test that kept a trace, by report result id, as a path from the project root.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
