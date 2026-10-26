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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L31)

## Fields

<a id="E2E_ReportExploreEnded_Aborted"></a>

### Aborted


```csharp
Aborted = 4
```


The run was cut short: a cancelled step, a failing provider, or a planner that failed twice in a row.

**Value:** [ReportExploreEnded](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L42)

<a id="E2E_ReportExploreEnded_Finished"></a>

### Finished


```csharp
Finished = 0
```


**Value:** [ReportExploreEnded](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L33)

<a id="E2E_ReportExploreEnded_StepLimit"></a>

### StepLimit


```csharp
StepLimit = 1
```


**Value:** [ReportExploreEnded](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L35)

<a id="E2E_ReportExploreEnded_Stuck"></a>

### Stuck


```csharp
Stuck = 3
```


**Value:** [ReportExploreEnded](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L39)

<a id="E2E_ReportExploreEnded_Time"></a>

### Time


```csharp
Time = 2
```


**Value:** [ReportExploreEnded](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L37)
