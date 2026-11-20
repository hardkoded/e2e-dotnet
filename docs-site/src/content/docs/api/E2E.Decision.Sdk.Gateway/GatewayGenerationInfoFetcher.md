---
title: "GatewayGenerationInfoFetcher"
description: "The AI SDK's GatewayGenerationInfoFetcher (gateway-generation-info.ts): GET {origin}/v1/generation?id=...."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayGenerationInfoFetcher"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoFetcher"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayGenerationInfoFetcher
```


The AI SDK's `GatewayGenerationInfoFetcher` (`gateway-generation-info.ts`): `GET {origin}/v1/generation?id=...`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayGenerationInfo.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoFetcher__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoFetcher__ctor_E2E_Decision_Sdk_Gateway_GatewayConfig_"></a>

### GatewayGenerationInfoFetcher(GatewayConfig)


```csharp
public GatewayGenerationInfoFetcher(GatewayConfig config)
```


The AI SDK's `GatewayGenerationInfoFetcher` (`gateway-generation-info.ts`): `GET {origin}/v1/generation?id=...`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `config` | [GatewayConfig](/api/E2E.Decision.Sdk.Gateway/GatewayConfig/) |  |

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoFetcher_GetGenerationInfoAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoFetcher_GetGenerationInfoAsync_E2E_Decision_Sdk_Gateway_GatewayGenerationInfoParams_System_Threading_CancellationToken_"></a>

### GetGenerationInfoAsync(GatewayGenerationInfoParams, CancellationToken)


```csharp
public Task<GatewayGenerationInfo> GetGenerationInfoAsync(GatewayGenerationInfoParams parameters, CancellationToken cancellationToken = default)
```


Upstream's `getGenerationInfo`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `parameters` | [GatewayGenerationInfoParams](/api/E2E.Decision.Sdk.Gateway/GatewayGenerationInfoParams/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayGenerationInfo&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
