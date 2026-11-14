---
title: "ReportExploreEnded"
description: "Why an exploration stopped."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExploreEnded"
  hidden: true
---

<a id="E2E_ReportExploreEnded"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ReportExploreEnded
```


Why an exploration stopped.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs)

## Fields

<a id="E2E_ReportExploreEnded_Aborted"></a>

### Aborted


```csharp
Aborted = 5
```


The run was cut short: a cancelled step, a failing provider, or a planner that failed twice in a row.

**Value:** [ReportExploreEnded](./)

<a id="E2E_ReportExploreEnded_Finished"></a>

### Finished


```csharp
Finished = 0
```


**Value:** [ReportExploreEnded](./)

<a id="E2E_ReportExploreEnded_Skipped"></a>

### Skipped


```csharp
Skipped = 1
```


The planner judged, before any step, that the change since `--changed-since` has nothing a user can see; the run passes.

**Value:** [ReportExploreEnded](./)

<a id="E2E_ReportExploreEnded_StepLimit"></a>

### StepLimit


```csharp
StepLimit = 2
```


**Value:** [ReportExploreEnded](./)

<a id="E2E_ReportExploreEnded_Stuck"></a>

### Stuck


```csharp
Stuck = 4
```


**Value:** [ReportExploreEnded](./)

<a id="E2E_ReportExploreEnded_Time"></a>

### Time


```csharp
Time = 3
```


**Value:** [ReportExploreEnded](./)
