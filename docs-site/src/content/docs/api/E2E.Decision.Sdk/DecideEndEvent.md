---
title: "DecideEndEvent"
description: "The event passed to OnEnd (the AI SDK's DecideEndEvent), when the operation completes successfully. The result's response is flattened as on DecisionResult."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecideEndEvent"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecideEndEvent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public record DecideEndEvent : DecideStartEvent, IEquatable<TelemetryEvent>, IEquatable<DecideStartEvent>, IEquatable<DecideEndEvent>
```


The event passed to [OnEnd](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_OnEnd) (the AI SDK's `DecideEndEvent`), when the operation completes successfully.
The result's `response` is flattened as on [DecisionResult](/api/E2E.Decision.Sdk/DecisionResult/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/) → [DecideStartEvent](/api/E2E.Decision.Sdk/DecideStartEvent/)

**Implements:** [IEquatable&lt;TelemetryEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecideStartEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecideEndEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecideEvents.cs)

## Properties

<a id="E2E_Decision_Sdk_DecideEndEvent_Answers_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_Answers"></a>

### Answers


```csharp
public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }
```


Exactly one typed answer per question ID.

**Value:** [IReadOnlyDictionary&lt;string, DecisionAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideEndEvent_ProviderMetadata_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public JsonElement? ProviderMetadata { get; init; }
```


Optional provider-specific metadata.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseBody_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseBody"></a>

### ResponseBody


```csharp
public JsonElement? ResponseBody { get; init; }
```


The provider's response body, as it sent it (upstream's `response.body`).

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseHeaders_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseHeaders"></a>

### ResponseHeaders


```csharp
public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }
```


The provider's response headers (upstream's `response.headers`), lowercase names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseId_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseId"></a>

### ResponseId


```csharp
public string? ResponseId { get; init; }
```


The ID of the response (upstream's `response.id`), when the provider sends one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseModelId_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_ResponseModelId"></a>

### ResponseModelId


```csharp
public required string ResponseModelId { get; init; }
```


The resolved model ID of the response.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideEndEvent_Rounding_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_Rounding"></a>

### Rounding


```csharp
public DecisionRounding? Rounding { get; init; }
```


Provider-declared decimal precision for probabilities and scores.

**Value:** [DecisionRounding](/api/E2E.Decision.Sdk/DecisionRounding/)

<a id="E2E_Decision_Sdk_DecideEndEvent_Timestamp_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_Timestamp"></a>

### Timestamp


```csharp
public required DateTimeOffset Timestamp { get; init; }
```


The timestamp of the response.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Decision_Sdk_DecideEndEvent_Usage_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_Usage"></a>

### Usage


```csharp
public required DecisionUsage Usage { get; init; }
```


Token usage for the decision operation.

**Value:** [DecisionUsage](/api/E2E.Decision.Sdk/DecisionUsage/)

<a id="E2E_Decision_Sdk_DecideEndEvent_Warnings_"></a>
<a id="E2E_Decision_Sdk_DecideEndEvent_Warnings"></a>

### Warnings


```csharp
public required IReadOnlyList<string> Warnings { get; init; }
```


Warnings from the decision model.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
