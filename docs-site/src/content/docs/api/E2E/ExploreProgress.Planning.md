---
title: "ExploreProgress.Planning"
description: "The planner is deciding the next charter, or (Closing) the assessment that ends the run."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress.Planning"
  hidden: true
---

<a id="E2E_ExploreProgress_Planning"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreProgress.Planning : ExploreProgress, IEquatable<ExploreProgress>, IEquatable<ExploreProgress.Planning>
```


The planner is deciding the next charter, or (&lt;code class="paramref"&gt;Closing&lt;/code&gt;) the assessment that ends the run.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ExploreProgress](/api/E2E/ExploreProgress/)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreProgress.Planning&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L33)

## Constructors

<a id="E2E_ExploreProgress_Planning__ctor_"></a>
<a id="E2E_ExploreProgress_Planning__ctor_System_Boolean_"></a>

### Planning(bool)


```csharp
public Planning(bool Closing)
```


The planner is deciding the next charter, or (&lt;code class="paramref"&gt;Closing&lt;/code&gt;) the assessment that ends the run.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Closing` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L33)

## Properties

<a id="E2E_ExploreProgress_Planning_Closing_"></a>
<a id="E2E_ExploreProgress_Planning_Closing"></a>

### Closing


```csharp
public bool Closing { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L33)
