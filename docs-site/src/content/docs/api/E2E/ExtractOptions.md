---
title: "ExtractOptions"
description: "agent.extract: one extraction and one repair round. The type argument of CancellationToken) is the schema."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExtractOptions"
  hidden: true
---

<a id="E2E_ExtractOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExtractOptions
```


`agent.extract`: one extraction and one repair round. The type argument of
`CancellationToken)` is the schema.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L995)

## Properties

<a id="E2E_ExtractOptions_Agent_"></a>
<a id="E2E_ExtractOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The named agent whose judge reads the screen. Defaults to `default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1004)

<a id="E2E_ExtractOptions_Timeout_"></a>
<a id="E2E_ExtractOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Defaults to the agent's `JudgmentTimeout` (30 s).

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L998)

<a id="E2E_ExtractOptions_Vision_"></a>
<a id="E2E_ExtractOptions_Vision"></a>

### Vision


```csharp
public VisionMode Vision { get; init; }
```


What the judge reads the data from. [Off](/api/E2E/VisionMode/#E2E_VisionMode_Off) by default: the tree. See [VisionMode](/api/E2E/VisionMode/).

**Value:** [VisionMode](/api/E2E/VisionMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1001)
