---
title: "TelemetryEvent"
description: "What a telemetry integration adds to every event it receives (the AI SDK's augmentEvent of create-telemetry-dispatcher.ts): the call's telemetry settings and..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TelemetryEvent"
  hidden: true
---

<a id="E2E_Decision_Sdk_TelemetryEvent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public abstract record TelemetryEvent : IEquatable<TelemetryEvent>
```


What a telemetry integration adds to every event it receives (the AI SDK's `augmentEvent` of `create-telemetry-dispatcher.ts`):
the call's telemetry settings and the SDK version. Null on an event a caller's `OnStart` or `OnEnd` callback receives.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TelemetryEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [DecideStartEvent](/api/E2E.Decision.Sdk/DecideStartEvent/), [DecisionModelCallStartEvent](/api/E2E.Decision.Sdk/DecisionModelCallStartEvent/), [TelemetryErrorEvent](/api/E2E.Decision.Sdk/TelemetryErrorEvent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecideEvents.cs)

## Properties

<a id="E2E_Decision_Sdk_TelemetryEvent_AiVersion_"></a>
<a id="E2E_Decision_Sdk_TelemetryEvent_AiVersion"></a>

### AiVersion


```csharp
public string? AiVersion { get; init; }
```


The AI SDK version (`VERSION`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TelemetryEvent_FunctionId_"></a>
<a id="E2E_Decision_Sdk_TelemetryEvent_FunctionId"></a>

### FunctionId


```csharp
public string? FunctionId { get; init; }
```


The call's [FunctionId](/api/E2E.Decision.Sdk/TelemetryOptions/#E2E_Decision_Sdk_TelemetryOptions_FunctionId).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TelemetryEvent_RecordInputs_"></a>
<a id="E2E_Decision_Sdk_TelemetryEvent_RecordInputs"></a>

### RecordInputs


```csharp
public bool? RecordInputs { get; init; }
```


Whether the call records inputs ([RecordInputs](/api/E2E.Decision.Sdk/TelemetryOptions/#E2E_Decision_Sdk_TelemetryOptions_RecordInputs)).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_TelemetryEvent_RecordOutputs_"></a>
<a id="E2E_Decision_Sdk_TelemetryEvent_RecordOutputs"></a>

### RecordOutputs


```csharp
public bool? RecordOutputs { get; init; }
```


Whether the call records outputs ([RecordOutputs](/api/E2E.Decision.Sdk/TelemetryOptions/#E2E_Decision_Sdk_TelemetryOptions_RecordOutputs)).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)
