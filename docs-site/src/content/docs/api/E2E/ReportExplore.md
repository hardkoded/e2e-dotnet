---
title: "ReportExplore"
description: "What an exploration was asked, did, and found: upstream's run.explore block of the report."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExplore"
  hidden: true
---

<a id="E2E_ReportExplore"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ReportExplore : IEquatable<ReportExplore>
```


What an exploration was asked, did, and found: upstream's `run.explore` block of the report.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportExplore&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs)

## Constructors

<a id="E2E_ReportExplore__ctor_"></a>
<a id="E2E_ReportExplore__ctor_System_String_E2E_ExploreBudgets_E2E_ReportExploreEnded_System_Collections_Generic_IReadOnlyList_E2E_ReportExploreStep__System_Collections_Generic_IReadOnlyList_E2E_ReportExploreFinding__System_String_E2E_ReportExploreChange_"></a>

### `ReportExplore(string, ExploreBudgets, ReportExploreEnded, IReadOnlyList<ReportExploreStep>, IReadOnlyList<ReportExploreFinding>, string?, ReportExploreChange?)`


```csharp
public ReportExplore(string Goal, ExploreBudgets Budgets, ReportExploreEnded Ended, IReadOnlyList<ReportExploreStep> Steps, IReadOnlyList<ReportExploreFinding> Findings, string? Summary = null, ReportExploreChange? Change = null)
```


What an exploration was asked, did, and found: upstream's `run.explore` block of the report.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Goal` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The goal, with the secret values it spelled out redacted. |
| `Budgets` | [ExploreBudgets](/api/E2E/ExploreBudgets/) | The step cap and the clock the exploration ran under. |
| `Ended` | [ReportExploreEnded](/api/E2E/ReportExploreEnded/) | Why exploration stopped. |
| `Steps` | [IReadOnlyList&lt;ReportExploreStep&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The steps the agent planned and ran, in order. |
| `Findings` | [IReadOnlyList&lt;ReportExploreFinding&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The findings the agent reported, in order. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The agent's closing assessment, when it gave one, at most 4000 characters. |
| `Change` | [ReportExploreChange](/api/E2E/ReportExploreChange/) | The code change the exploration planned from, with `--changed-since`. |

## Properties

<a id="E2E_ReportExplore_Budgets_"></a>
<a id="E2E_ReportExplore_Budgets"></a>

### Budgets


```csharp
public ExploreBudgets Budgets { get; init; }
```


The step cap and the clock the exploration ran under.

**Value:** [ExploreBudgets](/api/E2E/ExploreBudgets/)

<a id="E2E_ReportExplore_Change_"></a>
<a id="E2E_ReportExplore_Change"></a>

### Change


```csharp
public ReportExploreChange? Change { get; init; }
```


The code change the exploration planned from, with `--changed-since`.

**Value:** [ReportExploreChange](/api/E2E/ReportExploreChange/)

<a id="E2E_ReportExplore_Ended_"></a>
<a id="E2E_ReportExplore_Ended"></a>

### Ended


```csharp
public ReportExploreEnded Ended { get; init; }
```


Why exploration stopped.

**Value:** [ReportExploreEnded](/api/E2E/ReportExploreEnded/)

<a id="E2E_ReportExplore_Findings_"></a>
<a id="E2E_ReportExplore_Findings"></a>

### Findings


```csharp
public IReadOnlyList<ReportExploreFinding> Findings { get; init; }
```


The findings the agent reported, in order.

**Value:** [IReadOnlyList&lt;ReportExploreFinding&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_ReportExplore_Goal_"></a>
<a id="E2E_ReportExplore_Goal"></a>

### Goal


```csharp
public string Goal { get; init; }
```


The goal, with the secret values it spelled out redacted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ReportExplore_Steps_"></a>
<a id="E2E_ReportExplore_Steps"></a>

### Steps


```csharp
public IReadOnlyList<ReportExploreStep> Steps { get; init; }
```


The steps the agent planned and ran, in order.

**Value:** [IReadOnlyList&lt;ReportExploreStep&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_ReportExplore_Summary_"></a>
<a id="E2E_ReportExplore_Summary"></a>

### Summary


```csharp
public string? Summary { get; init; }
```


The agent's closing assessment, when it gave one, at most 4000 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
