---
title: "ReportExploreStepStatus"
description: "How an exploration step ended."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExploreStepStatus"
  hidden: true
---

<a id="E2E_ReportExploreStepStatus"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ReportExploreStepStatus
```


How an exploration step ended.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L18)

## Fields

<a id="E2E_ReportExploreStepStatus_Blocked"></a>

### Blocked


```csharp
Blocked = 2
```


**Value:** [ReportExploreStepStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L24)

<a id="E2E_ReportExploreStepStatus_Exhausted"></a>

### Exhausted


```csharp
Exhausted = 3
```


The step ended at its action or time budget, which is not a failure.

**Value:** [ReportExploreStepStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L27)

<a id="E2E_ReportExploreStepStatus_Failed"></a>

### Failed


```csharp
Failed = 1
```


**Value:** [ReportExploreStepStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L22)

<a id="E2E_ReportExploreStepStatus_Passed"></a>

### Passed


```csharp
Passed = 0
```


**Value:** [ReportExploreStepStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L20)
