---
title: "ExploreProgress"
description: "The exploration's progress, one moment at a time: the step in progress, each finding the moment it is reported, and the end. ReportExplore is the record; the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress"
  hidden: true
---

<a id="E2E_ExploreProgress"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public abstract record ExploreProgress : IEquatable<ExploreProgress>
```


The exploration's progress, one moment at a time: the step in progress, each finding the moment it is reported, and the
end. [ReportExplore](/api/E2E/ReportExplore/) is the record; these are its moments. Upstream carries them as `explore` run
events, which its list reporter renders; the port has no run events, so [OnProgress](/api/E2E/ExploreOptions/#E2E_ExploreOptions_OnProgress) receives them.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [ExploreProgress.FindingReported](/api/E2E/ExploreProgress.FindingReported/), [ExploreProgress.Finished](/api/E2E/ExploreProgress.Finished/), [ExploreProgress.Planning](/api/E2E/ExploreProgress.Planning/), [ExploreProgress.Started](/api/E2E/ExploreProgress.Started/), [ExploreProgress.StepFinished](/api/E2E/ExploreProgress.StepFinished/), [ExploreProgress.StepStarted](/api/E2E/ExploreProgress.StepStarted/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L23)
