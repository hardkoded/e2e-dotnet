---
title: "ResolveModel"
description: "The AI SDK's packages/ai/src/model/resolve-model.ts (resolveDecisionModel, asEvaluationModel)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResolveModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_ResolveModel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class ResolveModel
```


The AI SDK's `packages/ai/src/model/resolve-model.ts` (`resolveDecisionModel`, `asEvaluationModel`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ResolveModel.cs)

## Properties

<a id="E2E_Decision_Sdk_ResolveModel_AiSdkDefaultProvider_"></a>
<a id="E2E_Decision_Sdk_ResolveModel_AiSdkDefaultProvider"></a>

### AiSdkDefaultProvider


```csharp
public static IDecisionProvider? AiSdkDefaultProvider { get; set; }
```


The AI SDK's `globalThis.AI_SDK_DEFAULT_PROVIDER`: the provider that resolves a model id. Null means the Gateway.
Process-wide, as upstream's global.

**Value:** [IDecisionProvider](/api/E2E.Decision.Sdk/IDecisionProvider/)

## Methods

<a id="E2E_Decision_Sdk_ResolveModel_AsEvaluationModel_"></a>
<a id="E2E_Decision_Sdk_ResolveModel_AsEvaluationModel_E2E_Decision_Sdk_IDecisionModel_"></a>

### AsEvaluationModel(IDecisionModel)


```csharp
public static EvaluationDecisionModel AsEvaluationModel(IDecisionModel model)
```


The AI SDK's `asEvaluationModel`: internal adapter for deprecated provider factories.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/) |  |

**Returns:** [EvaluationDecisionModel](/api/E2E.Decision.Sdk/EvaluationDecisionModel/)

<a id="E2E_Decision_Sdk_ResolveModel_ResolveDecisionModel_"></a>
<a id="E2E_Decision_Sdk_ResolveModel_ResolveDecisionModel_E2E_Decision_Sdk_IEvaluationModel_"></a>

### ResolveDecisionModel(IEvaluationModel)


```csharp
public static IDecisionModel ResolveDecisionModel(IEvaluationModel model)
```


The AI SDK's `resolveDecisionModel` for a deprecated evaluation model: an [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/) whose
`DoDecideAsync` calls the model's `DoEvaluateAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/) |  |

**Returns:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_Sdk_ResolveModel_ResolveDecisionModel_System_String_"></a>

### ResolveDecisionModel(string)


```csharp
public static IDecisionModel ResolveDecisionModel(string modelId)
```


The AI SDK's `resolveDecisionModel` for a model id: the default provider's `decisionModel`, else its deprecated
`evaluationModel`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)
