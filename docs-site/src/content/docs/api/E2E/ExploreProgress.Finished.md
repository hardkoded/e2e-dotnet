---
title: "ExploreProgress.Finished"
description: "ExploreProgress.Finished in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress.Finished"
  hidden: true
---

<a id="E2E_ExploreProgress_Finished"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreProgress.Finished : ExploreProgress, IEquatable<ExploreProgress>, IEquatable<ExploreProgress.Finished>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ExploreProgress](/api/E2E/ExploreProgress/)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreProgress.Finished&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L42)

## Constructors

<a id="E2E_ExploreProgress_Finished__ctor_"></a>
<a id="E2E_ExploreProgress_Finished__ctor_E2E_ReportExploreEnded_System_String_"></a>

### Finished(ReportExploreEnded, string?)


```csharp
public Finished(ReportExploreEnded Ended, string? Summary = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Ended` | [ReportExploreEnded](/api/E2E/ReportExploreEnded/) |  |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L42)

## Properties

<a id="E2E_ExploreProgress_Finished_Ended_"></a>
<a id="E2E_ExploreProgress_Finished_Ended"></a>

### Ended


```csharp
public ReportExploreEnded Ended { get; init; }
```


**Value:** [ReportExploreEnded](/api/E2E/ReportExploreEnded/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L42)

<a id="E2E_ExploreProgress_Finished_Summary_"></a>
<a id="E2E_ExploreProgress_Finished_Summary"></a>

### Summary


```csharp
public string? Summary { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L42)
