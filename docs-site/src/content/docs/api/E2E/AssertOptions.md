---
title: "AssertOptions"
description: "agent.assert: one judgment and one repair round, within Timeout."
editUrl: false
prev: false
next: false
sidebar:
  label: "AssertOptions"
  hidden: true
---

<a id="E2E_AssertOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AssertOptions
```


`agent.assert`: one judgment and one repair round, within [Timeout](#E2E_AssertOptions_Timeout).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L954)

## Properties

<a id="E2E_AssertOptions_Agent_"></a>
<a id="E2E_AssertOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The named agent whose judge decides. Defaults to `default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L969)

<a id="E2E_AssertOptions_Screenshot_"></a>
<a id="E2E_AssertOptions_Screenshot"></a>

### Screenshot


```csharp
public bool? Screenshot { get; init; }
```


Keep a redacted screenshot of the judged screen under the attempt's results, and name it in
[Screenshot](/api/E2E/AgentException/#E2E_AgentException_Screenshot) when the judgment fails. On by default.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L966)

<a id="E2E_AssertOptions_Timeout_"></a>
<a id="E2E_AssertOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Defaults to the agent's `JudgmentTimeout` (30 s).

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L957)

<a id="E2E_AssertOptions_Vision_"></a>
<a id="E2E_AssertOptions_Vision"></a>

### Vision


```csharp
public VisionMode Vision { get; init; }
```


What the judge is shown. [Off](/api/E2E/VisionMode/#E2E_VisionMode_Off) by default: the tree. See [VisionMode](/api/E2E/VisionMode/).

**Value:** [VisionMode](/api/E2E/VisionMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L960)
