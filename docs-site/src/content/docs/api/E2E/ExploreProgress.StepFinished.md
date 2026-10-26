---
title: "ExploreProgress.StepFinished"
description: "ExploreProgress.StepFinished in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress.StepFinished"
  hidden: true
---

<a id="E2E_ExploreProgress_StepFinished"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreProgress.StepFinished : ExploreProgress, IEquatable<ExploreProgress>, IEquatable<ExploreProgress.StepFinished>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ExploreProgress](/api/E2E/ExploreProgress/)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreProgress.StepFinished&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L37)

## Constructors

<a id="E2E_ExploreProgress_StepFinished__ctor_"></a>
<a id="E2E_ExploreProgress_StepFinished__ctor_E2E_ReportExploreStep_"></a>

### StepFinished(ReportExploreStep)


```csharp
public StepFinished(ReportExploreStep Step)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Step` | [ReportExploreStep](/api/E2E/ReportExploreStep/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L37)

## Properties

<a id="E2E_ExploreProgress_StepFinished_Step_"></a>
<a id="E2E_ExploreProgress_StepFinished_Step"></a>

### Step


```csharp
public ReportExploreStep Step { get; init; }
```


**Value:** [ReportExploreStep](/api/E2E/ReportExploreStep/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L37)
