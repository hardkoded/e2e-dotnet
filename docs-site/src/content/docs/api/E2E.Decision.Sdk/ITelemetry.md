---
title: "ITelemetry"
description: "A telemetry integration (the decide members of the AI SDK's Telemetry, telemetry/telemetry.ts). Every member does nothing by default, so an integration overr..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ITelemetry"
  hidden: true
---

<a id="E2E_Decision_Sdk_ITelemetry"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface ITelemetry
```


A telemetry integration (the decide members of the AI SDK's `Telemetry`, `telemetry/telemetry.ts`). Every member does nothing by
default, so an integration overrides the ones it listens to.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Telemetry.cs)

## Methods

<a id="E2E_Decision_Sdk_ITelemetry_OnDecideEndAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnDecideEndAsync_E2E_Decision_Sdk_DecideEndEvent_"></a>

### OnDecideEndAsync(DecideEndEvent)


```csharp
Task OnDecideEndAsync(DecideEndEvent @event)
```


Called when an experimental decision operation completes (`experimental_onDecideEnd`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecideEndEvent](/api/E2E.Decision.Sdk/DecideEndEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Decision_Sdk_ITelemetry_OnDecideStartAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnDecideStartAsync_E2E_Decision_Sdk_DecideStartEvent_"></a>

### OnDecideStartAsync(DecideStartEvent)


```csharp
Task OnDecideStartAsync(DecideStartEvent @event)
```


Called when an experimental decision operation begins (`experimental_onDecideStart`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecideStartEvent](/api/E2E.Decision.Sdk/DecideStartEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Decision_Sdk_ITelemetry_OnDecisionModelCallEndAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnDecisionModelCallEndAsync_E2E_Decision_Sdk_DecisionModelCallEndEvent_"></a>

### OnDecisionModelCallEndAsync(DecisionModelCallEndEvent)


```csharp
Task OnDecisionModelCallEndAsync(DecisionModelCallEndEvent @event)
```


Called after an experimental decision model response has been validated (`experimental_onDecisionModelCallEnd`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecisionModelCallEndEvent](/api/E2E.Decision.Sdk/DecisionModelCallEndEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Decision_Sdk_ITelemetry_OnDecisionModelCallStartAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnDecisionModelCallStartAsync_E2E_Decision_Sdk_DecisionModelCallStartEvent_"></a>

### OnDecisionModelCallStartAsync(DecisionModelCallStartEvent)


```csharp
Task OnDecisionModelCallStartAsync(DecisionModelCallStartEvent @event)
```


Called immediately before an experimental decision model call begins (`experimental_onDecisionModelCallStart`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecisionModelCallStartEvent](/api/E2E.Decision.Sdk/DecisionModelCallStartEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Decision_Sdk_ITelemetry_OnEndAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnEndAsync_E2E_Decision_Sdk_TelemetryEvent_"></a>

### OnEndAsync(TelemetryEvent)


```csharp
Task OnEndAsync(TelemetryEvent @event)
```


Called when an operation completes (`onEnd`). Decide does not call it: it has [OnDecideEndAsync(DecideEndEvent)](#E2E_Decision_Sdk_ITelemetry_OnDecideEndAsync_E2E_Decision_Sdk_DecideEndEvent_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Decision_Sdk_ITelemetry_OnErrorAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnErrorAsync_E2E_Decision_Sdk_TelemetryErrorEvent_"></a>

### OnErrorAsync(TelemetryErrorEvent)


```csharp
Task OnErrorAsync(TelemetryErrorEvent @event)
```


Called when the operation fails (`onError`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [TelemetryErrorEvent](/api/E2E.Decision.Sdk/TelemetryErrorEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluateEndAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluateEndAsync_E2E_Decision_Sdk_DecideEndEvent_"></a>

### OnEvaluateEndAsync(DecideEndEvent)


```csharp
[Obsolete("Use OnDecideEndAsync.")]
Task OnEvaluateEndAsync(DecideEndEvent @event)
```


Deprecated alias of [OnDecideEndAsync(DecideEndEvent)](#E2E_Decision_Sdk_ITelemetry_OnDecideEndAsync_E2E_Decision_Sdk_DecideEndEvent_) (`experimental_onEvaluateEnd`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecideEndEvent](/api/E2E.Decision.Sdk/DecideEndEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

:::caution[Obsolete]
This API is obsolete.
:::

<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluateStartAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluateStartAsync_E2E_Decision_Sdk_DecideStartEvent_"></a>

### OnEvaluateStartAsync(DecideStartEvent)


```csharp
[Obsolete("Use OnDecideStartAsync.")]
Task OnEvaluateStartAsync(DecideStartEvent @event)
```


Deprecated alias of [OnDecideStartAsync(DecideStartEvent)](#E2E_Decision_Sdk_ITelemetry_OnDecideStartAsync_E2E_Decision_Sdk_DecideStartEvent_) (`experimental_onEvaluateStart`); ignored when the integration implements the current member.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecideStartEvent](/api/E2E.Decision.Sdk/DecideStartEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

:::caution[Obsolete]
This API is obsolete.
:::

<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluationModelCallEndAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluationModelCallEndAsync_E2E_Decision_Sdk_DecisionModelCallEndEvent_"></a>

### OnEvaluationModelCallEndAsync(DecisionModelCallEndEvent)


```csharp
[Obsolete("Use OnDecisionModelCallEndAsync.")]
Task OnEvaluationModelCallEndAsync(DecisionModelCallEndEvent @event)
```


Deprecated alias of [OnDecisionModelCallEndAsync(DecisionModelCallEndEvent)](#E2E_Decision_Sdk_ITelemetry_OnDecisionModelCallEndAsync_E2E_Decision_Sdk_DecisionModelCallEndEvent_) (`experimental_onEvaluationModelCallEnd`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecisionModelCallEndEvent](/api/E2E.Decision.Sdk/DecisionModelCallEndEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

:::caution[Obsolete]
This API is obsolete.
:::

<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluationModelCallStartAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnEvaluationModelCallStartAsync_E2E_Decision_Sdk_DecisionModelCallStartEvent_"></a>

### OnEvaluationModelCallStartAsync(DecisionModelCallStartEvent)


```csharp
[Obsolete("Use OnDecisionModelCallStartAsync.")]
Task OnEvaluationModelCallStartAsync(DecisionModelCallStartEvent @event)
```


Deprecated alias of [OnDecisionModelCallStartAsync(DecisionModelCallStartEvent)](#E2E_Decision_Sdk_ITelemetry_OnDecisionModelCallStartAsync_E2E_Decision_Sdk_DecisionModelCallStartEvent_) (`experimental_onEvaluationModelCallStart`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [DecisionModelCallStartEvent](/api/E2E.Decision.Sdk/DecisionModelCallStartEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

:::caution[Obsolete]
This API is obsolete.
:::

<a id="E2E_Decision_Sdk_ITelemetry_OnStartAsync_"></a>
<a id="E2E_Decision_Sdk_ITelemetry_OnStartAsync_E2E_Decision_Sdk_TelemetryEvent_"></a>

### OnStartAsync(TelemetryEvent)


```csharp
Task OnStartAsync(TelemetryEvent @event)
```


Called when an operation begins (`onStart`). Upstream fires it for text, object, embedding, reranking, speech, and transcription
operations, whose events the port does not have. Decide does not call it: it has [OnDecideStartAsync(DecideStartEvent)](#E2E_Decision_Sdk_ITelemetry_OnDecideStartAsync_E2E_Decision_Sdk_DecideStartEvent_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `event` | [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
