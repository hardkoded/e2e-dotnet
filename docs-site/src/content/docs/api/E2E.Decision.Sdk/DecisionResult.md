---
title: "DecisionResult"
description: "What RunAsync returns (the AI SDK's DecisionResult)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionResult"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionResult"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionResult
```


What `RunAsync` returns (the AI SDK's `DecisionResult`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionResult_Answers_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_Answers"></a>

### Answers


```csharp
public required IReadOnlyDictionary<string, DecisionAnswer> Answers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, DecisionAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionResult_ProviderMetadata_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public JsonElement? ProviderMetadata { get; init; }
```


**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionResult_ResponseBody_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_ResponseBody"></a>

### ResponseBody


```csharp
public JsonElement? ResponseBody { get; init; }
```


The provider's response body, as it sent it (upstream's `response.body`).

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionResult_ResponseHeaders_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_ResponseHeaders"></a>

### ResponseHeaders


```csharp
public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }
```


The provider's response headers (upstream's `response.headers`), lowercase names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionResult_ResponseModelId_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_ResponseModelId"></a>

### ResponseModelId


```csharp
public required string ResponseModelId { get; init; }
```


The id of the model that served the call: the response's, or the requested one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionResult_Rounding_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_Rounding"></a>

### Rounding


```csharp
public DecisionRounding? Rounding { get; init; }
```


**Value:** [DecisionRounding](/api/E2E.Decision.Sdk/DecisionRounding/)

<a id="E2E_Decision_Sdk_DecisionResult_Timestamp_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_Timestamp"></a>

### Timestamp


```csharp
public DateTimeOffset Timestamp { get; init; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Decision_Sdk_DecisionResult_Usage_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_Usage"></a>

### Usage


```csharp
public required DecisionUsage Usage { get; init; }
```


**Value:** [DecisionUsage](/api/E2E.Decision.Sdk/DecisionUsage/)

<a id="E2E_Decision_Sdk_DecisionResult_Warnings_"></a>
<a id="E2E_Decision_Sdk_DecisionResult_Warnings"></a>

### Warnings


```csharp
public IReadOnlyList<string> Warnings { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
