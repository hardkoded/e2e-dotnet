---
title: "GatewayConfig"
description: "The AI SDK's GatewayConfig (gateway-config.ts): where and how a Gateway model sends its requests."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayConfig"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayConfig"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public class GatewayConfig
```


The AI SDK's `GatewayConfig` (`gateway-config.ts`): where and how a Gateway model sends its requests.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Derived:** [GatewayDecisionModelConfig](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModelConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayDecisionModel.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayConfig_BaseUrl_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayConfig_BaseUrl"></a>

### BaseUrl


```csharp
public required string BaseUrl { get; init; }
```


The base URL prefix for API calls.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayConfig_Headers_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayConfig_Headers"></a>

### Headers


```csharp
public Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>? Headers { get; init; }
```


Resolves the headers sent with every request, such as the `Authorization` header. A null value drops the header.

**Value:** [Func&lt;CancellationToken, Task&lt;IReadOnlyDictionary&lt;string, string&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Decision_Sdk_Gateway_GatewayConfig_HttpClient_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayConfig_HttpClient"></a>

### HttpClient


```csharp
public HttpClient? HttpClient { get; init; }
```


The client that sends the requests. Defaults to a client of the model's own, with a 120 second timeout.

**Value:** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)
