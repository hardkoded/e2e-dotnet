---
title: "AnthropicModelOptions"
description: "AnthropicModelOptions in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "AnthropicModelOptions"
  hidden: true
---

<a id="E2E_AnthropicModelOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AnthropicModelOptions
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L239)

## Properties

<a id="E2E_AnthropicModelOptions_ApiKey_"></a>
<a id="E2E_AnthropicModelOptions_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L249)

<a id="E2E_AnthropicModelOptions_ApiKeyEnv_"></a>
<a id="E2E_AnthropicModelOptions_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string ApiKeyEnv { get; init; }
```


Environment variable read when [ApiKey](#E2E_AnthropicModelOptions_ApiKey) is empty. Defaults to `ANTHROPIC_API_KEY`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L252)

<a id="E2E_AnthropicModelOptions_BaseUrl_"></a>
<a id="E2E_AnthropicModelOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string BaseUrl { get; init; }
```


The API root; requests go to `{BaseUrl}/messages`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L244)

<a id="E2E_AnthropicModelOptions_CacheBreakpoints_"></a>
<a id="E2E_AnthropicModelOptions_CacheBreakpoints"></a>

### CacheBreakpoints


```csharp
public bool CacheBreakpoints { get; init; }
```


Marks the system prompt and the newest message as cache breakpoints. On by default, as upstream.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L261)

<a id="E2E_AnthropicModelOptions_Headers_"></a>
<a id="E2E_AnthropicModelOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L263)

<a id="E2E_AnthropicModelOptions_MaxTokens_"></a>
<a id="E2E_AnthropicModelOptions_MaxTokens"></a>

### MaxTokens


```csharp
public int MaxTokens { get; init; }
```


The output cap each call sends. The Messages API requires one.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L258)

<a id="E2E_AnthropicModelOptions_Model_"></a>
<a id="E2E_AnthropicModelOptions_Model"></a>

### Model


```csharp
public required string Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L241)

<a id="E2E_AnthropicModelOptions_Provider_"></a>
<a id="E2E_AnthropicModelOptions_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


The key this client reads in [ProviderOptions](/api/E2E/ModelRequest/#E2E_ModelRequest_ProviderOptions). Its fields are added to the body as given.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L247)

<a id="E2E_AnthropicModelOptions_Version_"></a>
<a id="E2E_AnthropicModelOptions_Version"></a>

### Version


```csharp
public string Version { get; init; }
```


The `anthropic-version` header.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L255)

## Methods

<a id="E2E_AnthropicModelOptions_ResolveApiKey_"></a>
<a id="E2E_AnthropicModelOptions_ResolveApiKey"></a>

### ResolveApiKey()


```csharp
public string? ResolveApiKey()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L265)
