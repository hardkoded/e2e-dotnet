---
title: "ExploreProgress.Started"
description: "The exploration begins: its test has started and the app is about to open."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreProgress.Started"
  hidden: true
---

<a id="E2E_ExploreProgress_Started"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreProgress.Started : ExploreProgress, IEquatable<ExploreProgress>, IEquatable<ExploreProgress.Started>
```


The exploration begins: its test has started and the app is about to open.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ExploreProgress](/api/E2E/ExploreProgress/)

**Implements:** [IEquatable&lt;ExploreProgress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ExploreProgress.Started&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L30)

## Constructors

<a id="E2E_ExploreProgress_Started__ctor_"></a>
<a id="E2E_ExploreProgress_Started__ctor_System_String_E2E_ExploreBudgets_"></a>

### Started(string, ExploreBudgets)


```csharp
public Started(string Goal, ExploreBudgets Budgets)
```


The exploration begins: its test has started and the app is about to open.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Goal` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Budgets` | [ExploreBudgets](/api/E2E/ExploreBudgets/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L30)

## Properties

<a id="E2E_ExploreProgress_Started_Budgets_"></a>
<a id="E2E_ExploreProgress_Started_Budgets"></a>

### Budgets


```csharp
public ExploreBudgets Budgets { get; init; }
```


**Value:** [ExploreBudgets](/api/E2E/ExploreBudgets/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L30)

<a id="E2E_ExploreProgress_Started_Goal_"></a>
<a id="E2E_ExploreProgress_Started_Goal"></a>

### Goal


```csharp
public string Goal { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L30)
