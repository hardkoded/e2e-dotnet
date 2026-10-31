---
title: "OpenAiResponsesModelOptions"
description: "OpenAiResponsesModelOptions in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenAiResponsesModelOptions"
  hidden: true
---

<a id="E2E_OpenAiResponsesModelOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class OpenAiResponsesModelOptions
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L273)

## Properties

<a id="E2E_OpenAiResponsesModelOptions_ApiKey_"></a>
<a id="E2E_OpenAiResponsesModelOptions_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L283)

<a id="E2E_OpenAiResponsesModelOptions_ApiKeyEnv_"></a>
<a id="E2E_OpenAiResponsesModelOptions_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string ApiKeyEnv { get; init; }
```


Environment variable read when [ApiKey](#E2E_OpenAiResponsesModelOptions_ApiKey) is empty. Defaults to `OPENAI_API_KEY`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L286)

<a id="E2E_OpenAiResponsesModelOptions_ApiKeyHeader_"></a>
<a id="E2E_OpenAiResponsesModelOptions_ApiKeyHeader"></a>

### ApiKeyHeader


```csharp
public string? ApiKeyHeader { get; init; }
```


The header that carries the key. Null sends `Authorization: Bearer`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L289)

<a id="E2E_OpenAiResponsesModelOptions_BaseUrl_"></a>
<a id="E2E_OpenAiResponsesModelOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string BaseUrl { get; init; }
```


The API root; requests go to `{BaseUrl}/responses`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L278)

<a id="E2E_OpenAiResponsesModelOptions_Headers_"></a>
<a id="E2E_OpenAiResponsesModelOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L291)

<a id="E2E_OpenAiResponsesModelOptions_Model_"></a>
<a id="E2E_OpenAiResponsesModelOptions_Model"></a>

### Model


```csharp
public required string Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L275)

<a id="E2E_OpenAiResponsesModelOptions_PromptCacheHints_"></a>
<a id="E2E_OpenAiResponsesModelOptions_PromptCacheHints"></a>

### PromptCacheHints


```csharp
public bool PromptCacheHints { get; init; }
```


Whether requests carry a prompt-cache routing key, one per system prompt. Upstream sends it to OpenAI and Azure.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L296)

<a id="E2E_OpenAiResponsesModelOptions_Provider_"></a>
<a id="E2E_OpenAiResponsesModelOptions_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


The key this client reads in [ProviderOptions](/api/E2E/ModelRequest/#E2E_ModelRequest_ProviderOptions): `openai`, or `azure` for Azure OpenAI.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L281)

<a id="E2E_OpenAiResponsesModelOptions_QueryParameters_"></a>
<a id="E2E_OpenAiResponsesModelOptions_QueryParameters"></a>

### QueryParameters


```csharp
public IReadOnlyDictionary<string, string>? QueryParameters { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L293)

<a id="E2E_OpenAiResponsesModelOptions_SessionId_"></a>
<a id="E2E_OpenAiResponsesModelOptions_SessionId"></a>

### SessionId


```csharp
public string? SessionId { get; init; }
```


The routing key for a request with no system prompt, so promptless calls of one session share a cache and other sessions do not. Null hashes the empty prompt.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L299)

## Methods

<a id="E2E_OpenAiResponsesModelOptions_ResolveApiKey_"></a>
<a id="E2E_OpenAiResponsesModelOptions_ResolveApiKey"></a>

### ResolveApiKey()


```csharp
public string? ResolveApiKey()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L301)
