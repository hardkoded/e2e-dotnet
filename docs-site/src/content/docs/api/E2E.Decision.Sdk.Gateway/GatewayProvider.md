---
title: "GatewayProvider"
description: "The AI SDK's createGateway and the gateway provider (gateway-provider.ts), for its decision models: gateway.decisionModel('typesafe-ai/jev')."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayProvider"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayProvider : IDecisionProvider
```


The AI SDK's `createGateway` and the `gateway` provider (`gateway-provider.ts`), for its decision models:
`gateway.decisionModel('typesafe-ai/jev')`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionProvider](/api/E2E.Decision.Sdk/IDecisionProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayProvider.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider__ctor_E2E_Decision_Sdk_Gateway_GatewayProviderSettings_"></a>

### GatewayProvider(GatewayProviderSettings?)


```csharp
public GatewayProvider(GatewayProviderSettings? settings = null)
```


Upstream's `createGateway`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `settings` | [GatewayProviderSettings](/api/E2E.Decision.Sdk.Gateway/GatewayProviderSettings/) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_Gateway_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_Gateway"></a>

### Gateway


```csharp
public static GatewayProvider Gateway { get; }
```


The default provider (upstream's `export const gateway = createGateway()`).

**Value:** [GatewayProvider](./)

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_Decision_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_Decision_System_String_"></a>

### Decision(string)


```csharp
public GatewayDecisionModel Decision(string modelId)
```


Creates a decision model (upstream's `decision`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_DecisionModel_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_DecisionModel_System_String_"></a>

### DecisionModel(string)


```csharp
public GatewayDecisionModel DecisionModel(string modelId)
```


Creates a decision model (upstream's `decisionModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_Evaluation_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_Evaluation_System_String_"></a>

### Evaluation(string)


```csharp
public GatewayDecisionModel Evaluation(string modelId)
```


Deprecated: use [Decision(string)](#E2E_Decision_Sdk_Gateway_GatewayProvider_Decision_System_String_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_EvaluationModel_System_String_"></a>

### EvaluationModel(string)


```csharp
public GatewayDecisionModel EvaluationModel(string modelId)
```


Deprecated: use [DecisionModel(string)](#E2E_Decision_Sdk_Gateway_GatewayProvider_DecisionModel_System_String_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetAvailableModelsAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetAvailableModelsAsync_System_Threading_CancellationToken_"></a>

### GetAvailableModelsAsync(CancellationToken)


```csharp
public Task<GatewayFetchMetadataResponse> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
```


Returns available providers and models for use with the remote provider (upstream's `getAvailableModels`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayFetchMetadataResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetCreditsAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetCreditsAsync_System_Threading_CancellationToken_"></a>

### GetCreditsAsync(CancellationToken)


```csharp
public Task<GatewayCreditsResponse> GetCreditsAsync(CancellationToken cancellationToken = default)
```


Returns credit information for the authenticated user (upstream's `getCredits`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayCreditsResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetGatewayAuthTokenAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetGatewayAuthTokenAsync_E2E_Decision_Sdk_Gateway_GatewayProviderSettings_System_Threading_CancellationToken_"></a>

### GetGatewayAuthTokenAsync(GatewayProviderSettings, CancellationToken)


```csharp
public static Task<GatewayAuthToken> GetGatewayAuthTokenAsync(GatewayProviderSettings settings, CancellationToken cancellationToken = default)
```


Upstream's `getGatewayAuthToken`: a non-empty API key (the option, else `AI_GATEWAY_API_KEY`) is used as is, with no trimming;
otherwise the Vercel OIDC token. Callers wrap a failure here in a [GatewayAuthenticationException](/api/E2E.Decision.Sdk.Gateway/GatewayAuthenticationException/) ("No authentication provided").

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `settings` | [GatewayProviderSettings](/api/E2E.Decision.Sdk.Gateway/GatewayProviderSettings/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayAuthToken&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetGenerationInfoAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetGenerationInfoAsync_E2E_Decision_Sdk_Gateway_GatewayGenerationInfoParams_System_Threading_CancellationToken_"></a>

### GetGenerationInfoAsync(GatewayGenerationInfoParams, CancellationToken)


```csharp
public Task<GatewayGenerationInfo> GetGenerationInfoAsync(GatewayGenerationInfoParams parameters, CancellationToken cancellationToken = default)
```


Returns detailed information about a specific generation by its ID, including cost, token usage, latency, and provider details
(upstream's `getGenerationInfo`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `parameters` | [GatewayGenerationInfoParams](/api/E2E.Decision.Sdk.Gateway/GatewayGenerationInfoParams/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewayGenerationInfo&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetSpendReportAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProvider_GetSpendReportAsync_E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_System_Threading_CancellationToken_"></a>

### GetSpendReportAsync(GatewaySpendReportParams, CancellationToken)


```csharp
public Task<GatewaySpendReportResponse> GetSpendReportAsync(GatewaySpendReportParams parameters, CancellationToken cancellationToken = default)
```


Returns a spend report with cost, token, and request count data, aggregated by the specified dimension (upstream's
`getSpendReport`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `parameters` | [GatewaySpendReportParams](/api/E2E.Decision.Sdk.Gateway/GatewaySpendReportParams/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewaySpendReportResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
