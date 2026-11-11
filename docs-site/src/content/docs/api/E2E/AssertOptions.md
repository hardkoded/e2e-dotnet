---
title: "AssertOptions"
description: "agent.assert: one judgment and one repair round within Timeout, or opt-in bounded viewport exploration (Explore)."
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


`agent.assert`: one judgment and one repair round within [Timeout](#E2E_AssertOptions_Timeout), or opt-in bounded viewport exploration ([Explore](#E2E_AssertOptions_Explore)).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)

## Properties

<a id="E2E_AssertOptions_Agent_"></a>
<a id="E2E_AssertOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The named agent whose judge decides. Defaults to `default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_AssertOptions_Explore_"></a>
<a id="E2E_AssertOptions_Explore"></a>

### Explore


```csharp
public bool Explore { get; init; }
```


Look around by scrolling before judging; defaults to false. An exploring assertion offers `observe`, `scroll`,
`scroll_to`, and project tools declared read-only, and `screenshot` when [Vision](#E2E_AssertOptions_Vision) is not [Off](/api/E2E/VisionMode/#E2E_VisionMode_Off).
It uses the agent's `Model`, even when a `Judge` is configured. Insufficient evidence within the time or action and model
budget fails `ASSERTION_INCONCLUSIVE`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_AssertOptions_MaxModelCalls_"></a>
<a id="E2E_AssertOptions_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public int? MaxModelCalls { get; init; }
```


Model-call budget; requires [Explore](#E2E_AssertOptions_Explore) (`INVALID_ARGUMENT` otherwise). Defaults to min(6, the configured `MaxModelCalls`), and can only lower the configured limit.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_AssertOptions_MaxSteps_"></a>
<a id="E2E_AssertOptions_MaxSteps"></a>

### MaxSteps


```csharp
public int? MaxSteps { get; init; }
```


Scroll budget; requires [Explore](#E2E_AssertOptions_Explore) (`INVALID_ARGUMENT` otherwise). Defaults to min(10, the configured `MaxSteps`), and can only lower the configured limit.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_AssertOptions_Params_"></a>
<a id="E2E_AssertOptions_Params"></a>

### Params


```csharp
public IReadOnlyDictionary<string, object?>? Params { get; init; }
```


Values the assertion refers to as `{name}`, with the same rules as [Params](/api/E2E/ActOptions/#E2E_ActOptions_Params). The judge reads a
[Unique(string)](/api/E2E/Values/#E2E_Values_Unique_System_String_) value as its string and a [Secret](/api/E2E/Secret/) by name only, never its value.

**Value:** [IReadOnlyDictionary&lt;string, object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_AssertOptions_Screenshot_"></a>
<a id="E2E_AssertOptions_Screenshot"></a>

### Screenshot


```csharp
public bool? Screenshot { get; init; }
```


Keep a redacted screenshot of the judged screen under the attempt's results, and name it in
[Screenshot](/api/E2E/AgentException/#E2E_AgentException_Screenshot) when the judgment fails. On by default.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_AssertOptions_Timeout_"></a>
<a id="E2E_AssertOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Defaults to the agent's `JudgmentTimeout` (30 s).

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_AssertOptions_Vision_"></a>
<a id="E2E_AssertOptions_Vision"></a>

### Vision


```csharp
public VisionMode Vision { get; init; }
```


What the judge is shown. [Off](/api/E2E/VisionMode/#E2E_VisionMode_Off) by default: the tree. See [VisionMode](/api/E2E/VisionMode/).

**Value:** [VisionMode](/api/E2E/VisionMode/)
