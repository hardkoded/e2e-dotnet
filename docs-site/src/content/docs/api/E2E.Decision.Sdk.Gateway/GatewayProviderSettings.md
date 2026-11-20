---
title: "GatewayProviderSettings"
description: "The AI SDK's GatewayProviderSettings (gateway-provider.ts) the decision models read."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayProviderSettings"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayProviderSettings
```


The AI SDK's `GatewayProviderSettings` (`gateway-provider.ts`) the decision models read.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayProvider.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_ApiKey_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


The API key or Vercel access token sent as the `Authorization` bearer. Defaults to the `AI_GATEWAY_API_KEY` environment variable.
With neither set, the provider authenticates with the Vercel OIDC token of the `VERCEL_OIDC_TOKEN` environment variable.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_BaseUrl_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; init; }
```


The base URL prefix for API calls. Defaults to `https://ai-gateway.vercel.sh/v4/ai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_Headers_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Headers added to every request.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_HttpClient_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_HttpClient"></a>

### HttpClient


```csharp
public HttpClient? HttpClient { get; init; }
```


The client that sends the requests.

**Value:** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_MetadataCacheRefreshMillis_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_MetadataCacheRefreshMillis"></a>

### MetadataCacheRefreshMillis


```csharp
public double? MetadataCacheRefreshMillis { get; init; }
```


How frequently to refresh the metadata cache in milliseconds. Defaults to 5 minutes.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_TeamIdOrSlug_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderSettings_TeamIdOrSlug"></a>

### TeamIdOrSlug


```csharp
public string? TeamIdOrSlug { get; init; }
```


The Vercel team the requests are attributed to.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
