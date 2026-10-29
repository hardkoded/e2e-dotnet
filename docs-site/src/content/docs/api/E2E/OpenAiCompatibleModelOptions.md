---
title: "OpenAiCompatibleModelOptions"
description: "OpenAiCompatibleModelOptions in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenAiCompatibleModelOptions"
  hidden: true
---

<a id="E2E_OpenAiCompatibleModelOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class OpenAiCompatibleModelOptions
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L271)

## Properties

<a id="E2E_OpenAiCompatibleModelOptions_ApiKey_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L283)

<a id="E2E_OpenAiCompatibleModelOptions_ApiKeyEnv_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string ApiKeyEnv { get; init; }
```


Environment variable read when [ApiKey](#E2E_OpenAiCompatibleModelOptions_ApiKey) is empty. Defaults to `OPENAI_API_KEY`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L286)

<a id="E2E_OpenAiCompatibleModelOptions_ApiKeyFallbackEnv_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_ApiKeyFallbackEnv"></a>

### ApiKeyFallbackEnv


```csharp
public string? ApiKeyFallbackEnv { get; init; }
```


A second environment variable read when the first is unset, such as the Vercel OIDC token.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L289)

<a id="E2E_OpenAiCompatibleModelOptions_ApiKeyHeader_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_ApiKeyHeader"></a>

### ApiKeyHeader


```csharp
public string? ApiKeyHeader { get; init; }
```


The header that carries the key. Null (the default) sends `Authorization: Bearer`; Azure OpenAI
takes `api-key`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L295)

<a id="E2E_OpenAiCompatibleModelOptions_BaseUrl_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string BaseUrl { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L275)

<a id="E2E_OpenAiCompatibleModelOptions_Headers_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Headers added to every request. The identity headers (`User-Agent`, `HTTP-Referer`, `X-Title`) replace the same names set here.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L298)

<a id="E2E_OpenAiCompatibleModelOptions_Model_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_Model"></a>

### Model


```csharp
public required string Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L273)

<a id="E2E_OpenAiCompatibleModelOptions_PromptCacheHints_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_PromptCacheHints"></a>

### PromptCacheHints


```csharp
public bool? PromptCacheHints { get; init; }
```


Whether requests carry a prompt-cache key and `store: false`. Null (the default) sends them only to
`openai/*` models on the gateway, as upstream does; chat completions elsewhere get neither. Provider options win over both.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L307)

<a id="E2E_OpenAiCompatibleModelOptions_Provider_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


The key this client reads in [ProviderOptions](/api/E2E/ModelRequest/#E2E_ModelRequest_ProviderOptions). Its fields are added
to the chat-completions body as given. Defaults to `openai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L281)

<a id="E2E_OpenAiCompatibleModelOptions_QueryParameters_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_QueryParameters"></a>

### QueryParameters


```csharp
public IReadOnlyDictionary<string, string>? QueryParameters { get; init; }
```


Query parameters added to every request URL, such as Azure's `api-version`.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L301)

## Methods

<a id="E2E_OpenAiCompatibleModelOptions_ResolveApiKey_"></a>
<a id="E2E_OpenAiCompatibleModelOptions_ResolveApiKey"></a>

### ResolveApiKey()


```csharp
public string? ResolveApiKey()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L309)
