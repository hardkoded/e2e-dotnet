---
title: "DecisionModelCallEndEvent"
description: "The event fired after the decision model response has been validated (the AI SDK's DecisionModelCallEndEvent): the result of the logical model call, includin..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionModelCallEndEvent"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public record DecisionModelCallEndEvent : DecisionModelCallStartEvent, IEquatable<TelemetryEvent>, IEquatable<DecisionModelCallStartEvent>, IEquatable<DecisionModelCallEndEvent>
```


The event fired after the decision model response has been validated (the AI SDK's `DecisionModelCallEndEvent`): the result of the
logical model call, including any retries.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/) → [DecisionModelCallStartEvent](/api/E2E.Decision.Sdk/DecisionModelCallStartEvent/)

**Implements:** [IEquatable&lt;TelemetryEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionModelCallStartEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionModelCallEndEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecideEvents.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Answers_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Answers"></a>

### Answers


```csharp
public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }
```


Exactly one answer per question ID.

**Value:** [IReadOnlyDictionary&lt;string, DecisionAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_ProviderMetadata_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public JsonElement? ProviderMetadata { get; init; }
```


Optional provider-specific metadata.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_ResponseId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_ResponseId"></a>

### ResponseId


```csharp
public string? ResponseId { get; init; }
```


The id of the response (upstream's `response.id`), when the provider sends one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_ResponseModelId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_ResponseModelId"></a>

### ResponseModelId


```csharp
public string? ResponseModelId { get; init; }
```


The id of the model that served the call, when the response names one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Rounding_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Rounding"></a>

### Rounding


```csharp
public DecisionRounding? Rounding { get; init; }
```


Provider-declared decimal precision for probabilities and scores.

**Value:** [DecisionRounding](/api/E2E.Decision.Sdk/DecisionRounding/)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Timestamp_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Timestamp"></a>

### Timestamp


```csharp
public DateTimeOffset? Timestamp { get; init; }
```


The timestamp of the response (upstream's `response.timestamp`), when the provider sends one.

**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Usage_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Usage"></a>

### Usage


```csharp
public DecisionUsage? Usage { get; init; }
```


Token usage reported by the decision model.

**Value:** [DecisionUsage](/api/E2E.Decision.Sdk/DecisionUsage/)

<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Warnings_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCallEndEvent_Warnings"></a>

### Warnings


```csharp
public required IReadOnlyList<string> Warnings { get; init; }
```


Warnings from the decision model.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
