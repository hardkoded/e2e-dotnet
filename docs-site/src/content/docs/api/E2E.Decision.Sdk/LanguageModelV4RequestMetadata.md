---
title: "LanguageModelV4RequestMetadata"
description: "The request of a language model result: the request information, for telemetry and debugging."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4RequestMetadata"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4RequestMetadata"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class LanguageModelV4RequestMetadata
```


The `request` of a language model result: the request information, for telemetry and debugging.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4RequestMetadata_Body_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4RequestMetadata_Body"></a>

### Body


```csharp
public JsonNode? Body { get; init; }
```


The request HTTP body that was sent to the provider API.

**Value:** [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

<a id="E2E_Decision_Sdk_LanguageModelV4RequestMetadata_Url_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4RequestMetadata_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


The actual HTTP endpoint used for the request, when known.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
