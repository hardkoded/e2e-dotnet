---
title: "GoogleModelOptions"
description: "GoogleModelOptions in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "GoogleModelOptions"
  hidden: true
---

<a id="E2E_GoogleModelOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class GoogleModelOptions
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L271)

## Properties

<a id="E2E_GoogleModelOptions_ApiKey_"></a>
<a id="E2E_GoogleModelOptions_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L281)

<a id="E2E_GoogleModelOptions_ApiKeyEnv_"></a>
<a id="E2E_GoogleModelOptions_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string ApiKeyEnv { get; init; }
```


Environment variable read when [ApiKey](#E2E_GoogleModelOptions_ApiKey) is empty. Defaults to `GOOGLE_GENERATIVE_AI_API_KEY`, as the AI SDK.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L284)

<a id="E2E_GoogleModelOptions_BaseUrl_"></a>
<a id="E2E_GoogleModelOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string BaseUrl { get; init; }
```


The API root; requests go to `{BaseUrl}/models/{Model}:generateContent`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L276)

<a id="E2E_GoogleModelOptions_Headers_"></a>
<a id="E2E_GoogleModelOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L286)

<a id="E2E_GoogleModelOptions_Model_"></a>
<a id="E2E_GoogleModelOptions_Model"></a>

### Model


```csharp
public required string Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L273)

<a id="E2E_GoogleModelOptions_Provider_"></a>
<a id="E2E_GoogleModelOptions_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


The key this client reads in [ProviderOptions](/api/E2E/ModelRequest/#E2E_ModelRequest_ProviderOptions). Its fields (such as `generationConfig`) are added to the body.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L279)

## Methods

<a id="E2E_GoogleModelOptions_ResolveApiKey_"></a>
<a id="E2E_GoogleModelOptions_ResolveApiKey"></a>

### ResolveApiKey()


```csharp
public string? ResolveApiKey()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L288)
