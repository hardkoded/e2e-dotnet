---
title: "GatewayDecisionProvider"
description: ".NET-only: the AI SDK's createGateway decision-model factory, served by the Vercel.AI package's GatewayProvider instead of a port inside E2E.Decision. It mak..."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayDecisionProvider"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayDecisionProvider : IDecisionProvider
```


.NET-only: the AI SDK's `createGateway` decision-model factory, served by the `Vercel.AI` package's
`GatewayProvider` instead of a port inside E2E.Decision. It makes `decisionModel` (and the deprecated
`evaluationModel`) return an [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/) that [ExperimentalDecide](/api/E2E.Decision.Sdk/ExperimentalDecide/) and the decision executor accept.
The rest of the Gateway (language, realtime, batch, spend, credits) is `Vercel.AI`'s: reach it through [Gateway](#E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_Gateway).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionProvider](/api/E2E.Decision.Sdk/IDecisionProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayDecisionProvider.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider__ctor_Vercel_AI_Gateway_GatewayProvider_"></a>

### GatewayDecisionProvider(GatewayProvider?)


```csharp
public GatewayDecisionProvider(GatewayProvider? gateway = null)
```


Wraps &lt;code class="paramref"&gt;gateway&lt;/code&gt;, or a default Gateway (`AI_GATEWAY_API_KEY`, else `VERCEL_OIDC_TOKEN`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `gateway` | `GatewayProvider` |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_Gateway_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_Gateway"></a>

### Gateway


```csharp
public GatewayProvider Gateway { get; }
```


The `Vercel.AI` Gateway provider the decision models call through.

**Value:** `GatewayProvider`

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_DecisionModel_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_DecisionModel_System_String_"></a>

### DecisionModel(string)


```csharp
public IDecisionModel? DecisionModel(string modelId)
```


Creates a decision model (`decisionModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionProvider_EvaluationModel_System_String_"></a>

### EvaluationModel(string)


```csharp
public IEvaluationModel? EvaluationModel(string modelId)
```


Deprecated: use [DecisionModel(string)](/api/E2E.Decision.Sdk/IDecisionProvider/#E2E_Decision_Sdk_IDecisionProvider_DecisionModel_System_String_). Creates an evaluation model (`evaluationModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/)
