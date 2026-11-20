---
title: "IProviderRegistryProvider"
description: "The decision members of the AI SDK's ProviderRegistryProvider (provider-registry.ts): models by providerId:modelId."
editUrl: false
prev: false
next: false
sidebar:
  label: "IProviderRegistryProvider"
  hidden: true
---

<a id="E2E_Decision_Sdk_IProviderRegistryProvider"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface IProviderRegistryProvider
```


The decision members of the AI SDK's `ProviderRegistryProvider` (`provider-registry.ts`): models by `providerId:modelId`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ProviderRegistry.cs)

## Methods

<a id="E2E_Decision_Sdk_IProviderRegistryProvider_DecisionModel_"></a>
<a id="E2E_Decision_Sdk_IProviderRegistryProvider_DecisionModel_System_String_"></a>

### DecisionModel(string)


```csharp
IDecisionModel DecisionModel(string id)
```


Creates a decision model from a combined id, such as `typesafe-ai:jev-latest`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_Sdk_IProviderRegistryProvider_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_IProviderRegistryProvider_EvaluationModel_System_String_"></a>

### EvaluationModel(string)


```csharp
EvaluationDecisionModel EvaluationModel(string id)
```


Deprecated: use [DecisionModel(string)](#E2E_Decision_Sdk_IProviderRegistryProvider_DecisionModel_System_String_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [EvaluationDecisionModel](/api/E2E.Decision.Sdk/EvaluationDecisionModel/)
