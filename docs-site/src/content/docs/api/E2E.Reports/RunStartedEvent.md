---
title: "RunStartedEvent"
description: "Upstream's run-started fact."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunStartedEvent"
  hidden: true
---

<a id="E2E_Reports_RunStartedEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record RunStartedEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<RunStartedEvent>
```


Upstream's `run-started` fact.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RunStartedEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Properties

<a id="E2E_Reports_RunStartedEvent_Agents_"></a>
<a id="E2E_Reports_RunStartedEvent_Agents"></a>

### Agents


```csharp
public IReadOnlyList<string>? Agents { get; init; }
```


The configured agents unpinned tests run as, when they are not `default` alone; null otherwise.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Reports_RunStartedEvent_ArtifactsRoot_"></a>
<a id="E2E_Reports_RunStartedEvent_ArtifactsRoot"></a>

### ArtifactsRoot


```csharp
public required string ArtifactsRoot { get; init; }
```


Absolute directory the report's artifact paths are relative to.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunStartedEvent_Ci_"></a>
<a id="E2E_Reports_RunStartedEvent_Ci"></a>

### Ci


```csharp
public bool Ci { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Reports_RunStartedEvent_Judge_"></a>
<a id="E2E_Reports_RunStartedEvent_Judge"></a>

### Judge


```csharp
public string? Judge { get; init; }
```


The run agent's judge, in the same form; null when judgments use [Model](#E2E_Reports_RunStartedEvent_Model).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunStartedEvent_Model_"></a>
<a id="E2E_Reports_RunStartedEvent_Model"></a>

### Model


```csharp
public string? Model { get; init; }
```


The run agent's model as `provider/model-id`; null when none is configured or the run names several agents.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunStartedEvent_ProjectId_"></a>
<a id="E2E_Reports_RunStartedEvent_ProjectId"></a>

### ProjectId


```csharp
public required string ProjectId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunStartedEvent_ProjectRoot_"></a>
<a id="E2E_Reports_RunStartedEvent_ProjectRoot"></a>

### ProjectRoot


```csharp
public required string ProjectRoot { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunStartedEvent_RunId_"></a>
<a id="E2E_Reports_RunStartedEvent_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_RunStartedEvent_Targets_"></a>
<a id="E2E_Reports_RunStartedEvent_Targets"></a>

### Targets


```csharp
public required IReadOnlyList<string> Targets { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
