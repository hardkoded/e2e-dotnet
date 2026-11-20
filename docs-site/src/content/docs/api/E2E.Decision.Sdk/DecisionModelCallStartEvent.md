---
title: "DecisionModelCallStartEvent"
description: "The event fired when the decision model call begins (the AI SDK's DecisionModelCallStartEvent). The logical model call includes any provider retries."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionModelCallStartEvent"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public record DecisionModelCallStartEvent : TelemetryEvent, IEquatable<TelemetryEvent>, IEquatable<DecisionModelCallStartEvent>
```


The event fired when the decision model call begins (the AI SDK's `DecisionModelCallStartEvent`). The logical model call includes any
provider retries.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/)

**Implements:** [IEquatable&lt;TelemetryEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionModelCallStartEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [DecisionModelCallEndEvent](/api/E2E.Decision.Sdk/DecisionModelCallEndEvent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecideEvents.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_CallId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_CallId"></a>

### CallId


```csharp
public required string CallId { get; init; }
```


Unique identifier for the outer decision call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_ModelId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_ModelId"></a>

### ModelId


```csharp
public required string ModelId { get; init; }
```


The decision model identifier.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_OperationId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_OperationId"></a>

### OperationId


```csharp
public string OperationId { get; }
```


Identifies the inner operation (`ai.decide.doDecide`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_Provider_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_Provider"></a>

### Provider


```csharp
public required string Provider { get; init; }
```


The provider identifier.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_Questions_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_Questions"></a>

### Questions


```csharp
public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }
```


The questions to decide against the shared state.

**Value:** [IReadOnlyDictionary&lt;string, DecisionQuestion&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_State_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallStartEvent_State"></a>

### State


```csharp
public required IReadOnlyList<DecisionStatePart> State { get; init; }
```


The shared state used to make decisions.

**Value:** [IReadOnlyList&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
