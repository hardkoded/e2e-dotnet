---
title: "IDecisionProvider"
description: "A provider that creates decision models (the AI SDK's DecisionProvider, ai/src/decide/decision-provider.ts). Both members are optional, as upstream's: a prov..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IDecisionProvider"
  hidden: true
---

<a id="E2E_Decision_Sdk_IDecisionProvider"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface IDecisionProvider
```


A provider that creates decision models (the AI SDK's `DecisionProvider`, `ai/src/decide/decision-provider.ts`). Both members
are optional, as upstream's: a provider overrides the ones it has. A member that is not overridden returns null.

**Derived:** [CustomProvider](/api/E2E.Decision.Sdk/CustomProvider/), [GatewayDecisionProvider](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionProvider.cs)

## Methods

<a id="E2E_Decision_Sdk_IDecisionProvider_DecisionModel_"></a>
<a id="E2E_Decision_Sdk_IDecisionProvider_DecisionModel_System_String_"></a>

### DecisionModel(string)


```csharp
IDecisionModel? DecisionModel(string modelId)
```


Creates a decision model (`decisionModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_Sdk_IDecisionProvider_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_IDecisionProvider_EvaluationModel_System_String_"></a>

### EvaluationModel(string)


```csharp
IEvaluationModel? EvaluationModel(string modelId)
```


Deprecated: use [DecisionModel(string)](#E2E_Decision_Sdk_IDecisionProvider_DecisionModel_System_String_). Creates an evaluation model (`evaluationModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/)
