---
title: "ExploreBudgets"
description: "What an exploration was given to spend: its step cap and its wall clock."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreBudgets"
  hidden: true
---

<a id="E2E_ExploreBudgets"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreBudgets : IEquatable<ExploreBudgets>
```


What an exploration was given to spend: its step cap and its wall clock.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExploreBudgets&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L10)

## Constructors

<a id="E2E_ExploreBudgets__ctor_"></a>
<a id="E2E_ExploreBudgets__ctor_System_Int32_System_TimeSpan_"></a>

### ExploreBudgets(int, TimeSpan)


```csharp
public ExploreBudgets(int MaxSteps, TimeSpan Timeout)
```


What an exploration was given to spend: its step cap and its wall clock.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `MaxSteps` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Exploration steps, upstream's `maxSteps`. |
| `Timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) | The run's wall clock, upstream's `timeoutMs`. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L10)

## Properties

<a id="E2E_ExploreBudgets_MaxSteps_"></a>
<a id="E2E_ExploreBudgets_MaxSteps"></a>

### MaxSteps


```csharp
public int MaxSteps { get; init; }
```


Exploration steps, upstream's `maxSteps`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L10)

<a id="E2E_ExploreBudgets_Timeout_"></a>
<a id="E2E_ExploreBudgets_Timeout"></a>

### Timeout


```csharp
public TimeSpan Timeout { get; init; }
```


The run's wall clock, upstream's `timeoutMs`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L10)
