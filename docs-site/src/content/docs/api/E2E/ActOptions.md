---
title: "ActOptions"
description: "Values passed into one CancellationToken) goal. Put fresh data in Unique(string)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActOptions"
  hidden: true
---

<a id="E2E_ActOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ActOptions
```


Values passed into one `CancellationToken)` goal. Put fresh data in [Unique(string)](/api/E2E/Values/#E2E_Values_Unique_System_String_).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L902)

## Properties

<a id="E2E_ActOptions_Agent_"></a>
<a id="E2E_ActOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The named agent that runs the step. Defaults to `default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L915)

<a id="E2E_ActOptions_MaxModelCalls_"></a>
<a id="E2E_ActOptions_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public int? MaxModelCalls { get; init; }
```


Model-call budget. Defaults to the configured `MaxModelCalls` and can only lower it.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L912)

<a id="E2E_ActOptions_MaxSteps_"></a>
<a id="E2E_ActOptions_MaxSteps"></a>

### MaxSteps


```csharp
public int? MaxSteps { get; init; }
```


Action budget. Defaults to the configured `MaxSteps` (25) and can only lower it.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L909)

<a id="E2E_ActOptions_OnAction_"></a>
<a id="E2E_ActOptions_OnAction"></a>

### OnAction


```csharp
public ActionHook? OnAction { get; init; }
```


Runs after each action the step completes, once the screen settles, and before the next one. It also runs for the
actions the replay cache replays. Awaited, and its time counts toward [Timeout](#E2E_ActOptions_Timeout). Observe only: drive
nothing from it. A throw fails the step.

**Value:** [ActionHook](/api/E2E/ActionHook/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L922)

<a id="E2E_ActOptions_Params_"></a>
<a id="E2E_ActOptions_Params"></a>

### Params


```csharp
public IReadOnlyDictionary<string, object?>? Params { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L904)

<a id="E2E_ActOptions_Timeout_"></a>
<a id="E2E_ActOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L906)
