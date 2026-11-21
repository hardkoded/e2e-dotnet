---
title: "DecisionModelResult"
description: "What a decision model answers a call with (the AI SDK's DecisionModelV4Result)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionModelResult"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionModelResult"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionModelResult
```


What a decision model answers a call with (the AI SDK's `DecisionModelV4Result`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionModelResult_Answers_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_Answers"></a>

### Answers


```csharp
public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }
```


The answers by question id.

**Value:** [IReadOnlyDictionary&lt;string, DecisionAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelResult_ProviderMetadata_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public JsonElement? ProviderMetadata { get; init; }
```


What the provider reported beside the answers, by provider name (for example the per-question `confidence`).

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseBody_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseBody"></a>

### ResponseBody


```csharp
public JsonElement? ResponseBody { get; init; }
```


The provider's response body, as it sent it (upstream's `response.body`).

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseHeaders_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseHeaders"></a>

### ResponseHeaders


```csharp
public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }
```


The provider's response headers (upstream's `response.headers`), lowercase names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseId"></a>

### ResponseId


```csharp
public string? ResponseId { get; init; }
```


The id of the response (upstream's `response.id`), when the provider sends one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseModelId_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseModelId"></a>

### ResponseModelId


```csharp
public string? ResponseModelId { get; init; }
```


The id of the model that served the call, when the response names one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseTimestamp_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_ResponseTimestamp"></a>

### ResponseTimestamp


```csharp
public DateTimeOffset? ResponseTimestamp { get; init; }
```


The timestamp of the response (upstream's `response.timestamp`), when the provider sends one.

**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Decision_Sdk_DecisionModelResult_Rounding_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_Rounding"></a>

### Rounding


```csharp
public DecisionRounding? Rounding { get; init; }
```


The rounding the numbers were reported at. Null when they were not rounded.

**Value:** [DecisionRounding](/api/E2E.Decision.Sdk/DecisionRounding/)

<a id="E2E_Decision_Sdk_DecisionModelResult_Usage_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_Usage"></a>

### Usage


```csharp
public DecisionUsage? Usage { get; init; }
```


**Value:** [DecisionUsage](/api/E2E.Decision.Sdk/DecisionUsage/)

<a id="E2E_Decision_Sdk_DecisionModelResult_Warnings_"></a>
<a id="E2E_Decision_Sdk_DecisionModelResult_Warnings"></a>

### Warnings


```csharp
public IReadOnlyList<string> Warnings { get; init; }
```


Settings the provider did not support, as text.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
