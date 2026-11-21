---
title: "GatewayFetchMetadata"
description: "The AI SDK's GatewayFetchMetadata (gateway-fetch-metadata.ts): the models and the credits of the Gateway."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayFetchMetadata"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayFetchMetadata
```


The AI SDK's `GatewayFetchMetadata` (`gateway-fetch-metadata.ts`): the models and the credits of the Gateway.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayFetchMetadata.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata__ctor_E2E_Decision_Sdk_Gateway_GatewayConfig_"></a>

### GatewayFetchMetadata(GatewayConfig)


```csharp
public GatewayFetchMetadata(GatewayConfig config)
```


The AI SDK's `GatewayFetchMetadata` (`gateway-fetch-metadata.ts`): the models and the credits of the Gateway.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `config` | [GatewayConfig](/api/E2E.Decision.Sdk.Gateway/GatewayConfig/) |  |

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata_GetAvailableModelsAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata_GetAvailableModelsAsync_System_Threading_CancellationToken_"></a>

### GetAvailableModelsAsync(CancellationToken)


```csharp
public Task<GatewayFetchMetadataResponse> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
```


Upstream's `getAvailableModels`: `GET {baseURL}/config`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayFetchMetadataResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata_GetCreditsAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadata_GetCreditsAsync_System_Threading_CancellationToken_"></a>

### GetCreditsAsync(CancellationToken)


```csharp
public Task<GatewayCreditsResponse> GetCreditsAsync(CancellationToken cancellationToken = default)
```


Upstream's `getCredits`: `GET {origin}/v1/credits`, scoped to the team of the team header.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayCreditsResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
