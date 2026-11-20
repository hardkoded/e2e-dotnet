---
title: "DecideStartEvent"
description: "The event passed to OnStart (the AI SDK's DecideStartEvent), when the operation begins, before the decision model is called."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecideStartEvent"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecideStartEvent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public record DecideStartEvent : TelemetryEvent, IEquatable<TelemetryEvent>, IEquatable<DecideStartEvent>
```


The event passed to [OnStart](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_OnStart) (the AI SDK's `DecideStartEvent`), when the operation begins, before the decision
model is called.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/)

**Implements:** [IEquatable&lt;TelemetryEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecideStartEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [DecideEndEvent](/api/E2E.Decision.Sdk/DecideEndEvent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecideEvents.cs)

## Properties

<a id="E2E_Decision_Sdk_DecideStartEvent_CallId_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_CallId"></a>

### CallId


```csharp
public required string CallId { get; init; }
```


Unique identifier for this decision call, used to correlate events.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideStartEvent_Headers_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Additional HTTP headers sent with the request.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideStartEvent_MaxOutputTokens_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_MaxOutputTokens"></a>

### MaxOutputTokens


```csharp
public int? MaxOutputTokens { get; init; }
```


Maximum output tokens requested for the decision.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecideStartEvent_MaxRetries_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_MaxRetries"></a>

### MaxRetries


```csharp
public required int MaxRetries { get; init; }
```


Maximum number of retries for the decision model call.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecideStartEvent_ModelId_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_ModelId"></a>

### ModelId


```csharp
public required string ModelId { get; init; }
```


The decision model identifier.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideStartEvent_OperationId_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_OperationId"></a>

### OperationId


```csharp
public string OperationId { get; }
```


Identifies the operation type (`ai.decide`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideStartEvent_Provider_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_Provider"></a>

### Provider


```csharp
public required string Provider { get; init; }
```


The provider identifier.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideStartEvent_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_ProviderOptions"></a>

### ProviderOptions


```csharp
public required IReadOnlyDictionary<string, JsonElement> ProviderOptions { get; init; }
```


Additional provider-specific options.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideStartEvent_Questions_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_Questions"></a>

### Questions


```csharp
public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }
```


The questions to decide against the shared state.

**Value:** [IReadOnlyDictionary&lt;string, DecisionQuestion&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideStartEvent_Reasoning_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_Reasoning"></a>

### Reasoning


```csharp
public string? Reasoning { get; init; }
```


Reasoning effort requested for the decision.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideStartEvent_RuntimeContext_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_RuntimeContext"></a>

### RuntimeContext


```csharp
public required IReadOnlyDictionary<string, object?> RuntimeContext { get; init; }
```


User-defined runtime context.

**Value:** [IReadOnlyDictionary&lt;string, object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideStartEvent_State_"></a>
<a id="E2E_Decision_Sdk_DecideStartEvent_State"></a>

### State


```csharp
public required IReadOnlyList<DecisionStatePart> State { get; init; }
```


The shared state used to make decisions.

**Value:** [IReadOnlyList&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
