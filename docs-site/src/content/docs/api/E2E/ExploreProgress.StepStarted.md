---
title: "ExploreProgress.StepStarted"
description: "ExploreProgress.StepStarted in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress.StepStarted"
  hidden: true
---

<a id="E2E_ExploreProgress_StepStarted"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreProgress.StepStarted : ExploreProgress, IEquatable<ExploreProgress>, IEquatable<ExploreProgress.StepStarted>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ExploreProgress](/api/E2E/ExploreProgress/)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreProgress.StepStarted&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L35)

## Constructors

<a id="E2E_ExploreProgress_StepStarted__ctor_"></a>
<a id="E2E_ExploreProgress_StepStarted__ctor_E2E_OpenedStep_"></a>

### StepStarted(OpenedStep)


```csharp
public StepStarted(OpenedStep Step)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Step` | [OpenedStep](/api/E2E/OpenedStep/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L35)

## Properties

<a id="E2E_ExploreProgress_StepStarted_Step_"></a>
<a id="E2E_ExploreProgress_StepStarted_Step"></a>

### Step


```csharp
public OpenedStep Step { get; init; }
```


**Value:** [OpenedStep](/api/E2E/OpenedStep/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L35)
