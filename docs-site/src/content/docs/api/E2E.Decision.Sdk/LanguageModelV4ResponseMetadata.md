---
title: "LanguageModelV4ResponseMetadata"
description: "The AI SDK's LanguageModelV4ResponseMetadata (language-model-v4-response-metadata.ts) with the headers and body of a result's response."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ResponseMetadata"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class LanguageModelV4ResponseMetadata
```


The AI SDK's `LanguageModelV4ResponseMetadata` (`language-model-v4-response-metadata.ts`) with the `headers` and `body`
of a result's `response`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Body_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Body"></a>

### Body


```csharp
public JsonElement? Body { get; init; }
```


The response HTTP body.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Headers_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


The response headers, lowercase names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Id_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Id"></a>

### Id


```csharp
public string? Id { get; init; }
```


The ID for the generated response, if the provider sends one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_ModelId_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_ModelId"></a>

### ModelId


```csharp
public string? ModelId { get; init; }
```


The ID of the response model that was used to generate the response, if the provider sends one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Timestamp_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseMetadata_Timestamp"></a>

### Timestamp


```csharp
public DateTimeOffset? Timestamp { get; init; }
```


The timestamp for the start of the generated response, if the provider sends one.

**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)
