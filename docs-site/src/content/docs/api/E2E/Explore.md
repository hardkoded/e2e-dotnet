---
title: "Explore"
description: "An exploration: a run whose one test is a goal. The config is loaded as a test run loads it, the exploration body runs as the run's only test, and within tha..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Explore"
  hidden: true
---

<a id="E2E_Explore"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class Explore
```


An exploration: a run whose one test is a goal. The config is loaded as a test run loads it, the exploration body runs as the
run's only test, and within that test alone the explorer takes the place of the agent the run uses. The exploration's progress
reaches [OnProgress](/api/E2E/ExploreOptions/#E2E_ExploreOptions_OnProgress), and its record is [Explore](/api/E2E/ExploreOutcome/#E2E_ExploreOutcome_Explore).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L120)

## Fields

<a id="E2E_Explore_DefaultGoal"></a>

### DefaultGoal


```csharp
public const string DefaultGoal = "Explore the app and find bugs"
```


The goal when none is given.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L123)

<a id="E2E_Explore_MaxGoalChars"></a>

### MaxGoalChars


```csharp
public const int MaxGoalChars = 2000
```


The most characters a goal may have once its secret values are redacted.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L132)

## Properties

<a id="E2E_Explore_StepBounds_"></a>
<a id="E2E_Explore_StepBounds"></a>

### StepBounds


```csharp
public static ExploreBounds<int> StepBounds { get; }
```


Exploration steps per run: breadth first, and diminishing returns come fast.

**Value:** `ExploreBounds&lt;int&gt;`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L151)

<a id="E2E_Explore_TimeoutBounds_"></a>
<a id="E2E_Explore_TimeoutBounds"></a>

### TimeoutBounds


```csharp
public static ExploreBounds<TimeSpan> TimeoutBounds { get; }
```


The run's wall clock: three to fifteen minutes.

**Value:** `ExploreBounds&lt;TimeSpan&gt;`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L154)

## Methods

<a id="E2E_Explore_ExploreAsync_"></a>
<a id="E2E_Explore_ExploreAsync_E2E_ExploreOptions_System_Threading_CancellationToken_"></a>

### ExploreAsync(ExploreOptions, CancellationToken)


```csharp
public static Task<ExploreOutcome> ExploreAsync(ExploreOptions options, CancellationToken cancellationToken = default)
```


Runs one exploration and returns the outcome with the exploration record. The config is resolved once here, with the agent
[Agent](/api/E2E/ExploreOptions/#E2E_ExploreOptions_Agent) names, so the target, the agent the explorer is built from, and the credential inventory are
what the run uses; a config that does not resolve, an unknown agent, or an unknown target is an error before anything starts,
as a bad flag is. A step the app or the model fails is not thrown: it ends the run, and the outcome carries the error. A failure of closing the attempt (the engine's finish hook) is not thrown either: it joins the outcome as a cleanup error, and its exit code (3 for an engine failure) outranks a verdict's 1, as a run's run errors do.
A cancelled &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; throws [OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception); the run's own timeout ends it as an outcome whose error is `CANCELLED`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ExploreOptions](/api/E2E/ExploreOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ExploreOutcome&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L163)
