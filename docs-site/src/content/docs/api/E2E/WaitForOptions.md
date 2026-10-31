---
title: "WaitForOptions"
description: "agent.waitFor: a judgment at most once per Interval, and only on a changed screen, until Timeout."
editUrl: false
prev: false
next: false
sidebar:
  label: "WaitForOptions"
  hidden: true
---

<a id="E2E_WaitForOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class WaitForOptions
```


`agent.waitFor`: a judgment at most once per [Interval](#E2E_WaitForOptions_Interval), and only on a changed screen, until [Timeout](#E2E_WaitForOptions_Timeout).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1046)

## Properties

<a id="E2E_WaitForOptions_Agent_"></a>
<a id="E2E_WaitForOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The named agent whose judge decides. Defaults to `default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1061)

<a id="E2E_WaitForOptions_Interval_"></a>
<a id="E2E_WaitForOptions_Interval"></a>

### Interval


```csharp
public TimeSpan? Interval { get; init; }
```


Least time between two judgments, 100 ms through 60 s. Defaults to 3 s.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1052)

<a id="E2E_WaitForOptions_MaxModelCalls_"></a>
<a id="E2E_WaitForOptions_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public int? MaxModelCalls { get; init; }
```


Model-call budget. Defaults to the configured `MaxModelCalls` and can only lower it.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1055)

<a id="E2E_WaitForOptions_Timeout_"></a>
<a id="E2E_WaitForOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Defaults to the agent's `JudgmentTimeout` (30 s).

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1049)

<a id="E2E_WaitForOptions_Vision_"></a>
<a id="E2E_WaitForOptions_Vision"></a>

### Vision


```csharp
public VisionMode Vision { get; init; }
```


What the judge is shown. [Off](/api/E2E/VisionMode/#E2E_VisionMode_Off) by default: the tree. See [VisionMode](/api/E2E/VisionMode/).

**Value:** [VisionMode](/api/E2E/VisionMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1058)
