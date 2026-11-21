---
title: "LanguageModelV4GenerateResult"
description: "The AI SDK's LanguageModelV4GenerateResult (language-model-v4-generate-result.ts) of the Gateway. The Gateway passes the response through z.any(), so Content..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4GenerateResult"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class LanguageModelV4GenerateResult
```


The AI SDK's `LanguageModelV4GenerateResult` (`language-model-v4-generate-result.ts`) of the Gateway. The Gateway passes the
response through `z.any()`, so [Content](#E2E_Decision_Sdk_LanguageModelV4GenerateResult_Content), [FinishReason](#E2E_Decision_Sdk_LanguageModelV4GenerateResult_FinishReason), [Usage](#E2E_Decision_Sdk_LanguageModelV4GenerateResult_Usage), and
[ProviderMetadata](#E2E_Decision_Sdk_LanguageModelV4GenerateResult_ProviderMetadata) are the JSON the Gateway sent, unvalidated, and any other member of the response is in
[AdditionalProperties](#E2E_Decision_Sdk_LanguageModelV4GenerateResult_AdditionalProperties).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_AdditionalProperties_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_AdditionalProperties"></a>

### AdditionalProperties


```csharp
public IReadOnlyDictionary<string, JsonElement> AdditionalProperties { get; init; }
```


The members of the Gateway's response that are not named above, such as `id`, `created`, and `model`.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Content_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Content"></a>

### Content


```csharp
public JsonElement? Content { get; init; }
```


The ordered content that the model generated, as the Gateway sent it.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_FinishReason_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_FinishReason"></a>

### FinishReason


```csharp
public JsonElement? FinishReason { get; init; }
```


The finish reason, as the Gateway sent it.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_ProviderMetadata_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public JsonElement? ProviderMetadata { get; init; }
```


Additional provider-specific metadata, as the Gateway sent it.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Request_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Request"></a>

### Request


```csharp
public LanguageModelV4RequestMetadata? Request { get; init; }
```


The request information, for telemetry and debugging.

**Value:** [LanguageModelV4RequestMetadata](/api/E2E.Decision.Sdk/LanguageModelV4RequestMetadata/)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Response_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Response"></a>

### Response


```csharp
public LanguageModelV4ResponseMetadata? Response { get; init; }
```


The response information, for telemetry and debugging.

**Value:** [LanguageModelV4ResponseMetadata](/api/E2E.Decision.Sdk/LanguageModelV4ResponseMetadata/)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_StructuredOutputText_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_StructuredOutputText"></a>

### StructuredOutputText


```csharp
public string? StructuredOutputText { get; init; }
```


The text to parse for structured JSON output when it differs from the concatenation of the text parts of the content.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Usage_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Usage"></a>

### Usage


```csharp
public JsonElement? Usage { get; init; }
```


The usage information, as the Gateway sent it.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Warnings_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4GenerateResult_Warnings"></a>

### Warnings


```csharp
public required IReadOnlyList<SharedV4Warning> Warnings { get; init; }
```


The warnings for the call, for example unsupported settings: the Gateway's, then the model's own.

**Value:** [IReadOnlyList&lt;SharedV4Warning&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
