---
title: "TypeSafeAiProviderSettings"
description: "The AI SDK's TypeSafeAiProviderSettings (typesafe-ai-provider.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeSafeAiProviderSettings"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed class TypeSafeAiProviderSettings
```


The AI SDK's `TypeSafeAiProviderSettings` (`typesafe-ai-provider.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiProvider.cs)

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_ApiKey_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


The API key, sent as the bearer. Defaults to the `TYPESAFE_AI_API_KEY` environment variable, read at each call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_BaseUrl_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; init; }
```


The base URL prefix for API calls. Defaults to the `TYPESAFE_AI_BASE_URL` environment variable, then `https://api.typesafe.ai/v1`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_Headers_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Headers added to every request.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_HttpClient_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_HttpClient"></a>

### HttpClient


```csharp
public HttpClient? HttpClient { get; init; }
```


The client that sends the requests.

**Value:** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)
