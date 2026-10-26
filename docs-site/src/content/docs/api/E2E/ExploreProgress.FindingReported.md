---
title: "ExploreProgress.FindingReported"
description: "One finding, as recorded; its evidence screenshot, when kept, is the one ArtifactId names. Upstream's finding phase."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress.FindingReported"
  hidden: true
---

<a id="E2E_ExploreProgress_FindingReported"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreProgress.FindingReported : ExploreProgress, IEquatable<ExploreProgress>, IEquatable<ExploreProgress.FindingReported>
```


One finding, as recorded; its evidence screenshot, when kept, is the one [ArtifactId](/api/E2E/ReportExploreFinding/#E2E_ReportExploreFinding_ArtifactId) names. Upstream's `finding` phase.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ExploreProgress](/api/E2E/ExploreProgress/)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreProgress.FindingReported&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L40)

## Constructors

<a id="E2E_ExploreProgress_FindingReported__ctor_"></a>
<a id="E2E_ExploreProgress_FindingReported__ctor_E2E_ReportExploreFinding_"></a>

### FindingReported(ReportExploreFinding)


```csharp
public FindingReported(ReportExploreFinding Finding)
```


One finding, as recorded; its evidence screenshot, when kept, is the one [ArtifactId](/api/E2E/ReportExploreFinding/#E2E_ReportExploreFinding_ArtifactId) names. Upstream's `finding` phase.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Finding` | [ReportExploreFinding](/api/E2E/ReportExploreFinding/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L40)

## Properties

<a id="E2E_ExploreProgress_FindingReported_Finding_"></a>
<a id="E2E_ExploreProgress_FindingReported_Finding"></a>

### Finding


```csharp
public ReportExploreFinding Finding { get; init; }
```


**Value:** [ReportExploreFinding](/api/E2E/ReportExploreFinding/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L40)
