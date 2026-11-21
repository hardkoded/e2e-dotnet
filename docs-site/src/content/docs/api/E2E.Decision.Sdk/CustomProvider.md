---
title: "CustomProvider"
description: "The decision models of the AI SDK's customProvider (custom-provider.ts): a provider that serves its own models and falls back to another provider."
editUrl: false
prev: false
next: false
sidebar:
  label: "CustomProvider"
  hidden: true
---

<a id="E2E_Decision_Sdk_CustomProvider"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class CustomProvider : IDecisionProvider
```


The decision models of the AI SDK's `customProvider` (`custom-provider.ts`): a provider that serves its own models and
falls back to another provider.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionProvider](/api/E2E.Decision.Sdk/IDecisionProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/CustomProvider.cs)

## Constructors

<a id="E2E_Decision_Sdk_CustomProvider__ctor_"></a>
<a id="E2E_Decision_Sdk_CustomProvider__ctor_E2E_Decision_Sdk_CustomProviderOptions_"></a>

### CustomProvider(CustomProviderOptions)


```csharp
public CustomProvider(CustomProviderOptions options)
```


Upstream's `customProvider`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [CustomProviderOptions](/api/E2E.Decision.Sdk/CustomProviderOptions/) |  |

## Methods

<a id="E2E_Decision_Sdk_CustomProvider_DecisionModel_"></a>
<a id="E2E_Decision_Sdk_CustomProvider_DecisionModel_System_String_"></a>

### DecisionModel(string)


```csharp
public IDecisionModel DecisionModel(string modelId)
```


Creates a decision model (upstream's `decisionModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_Sdk_CustomProvider_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_CustomProvider_EvaluationModel_System_String_"></a>

### EvaluationModel(string)


```csharp
public EvaluationDecisionModel EvaluationModel(string modelId)
```


Deprecated: use [DecisionModel(string)](#E2E_Decision_Sdk_CustomProvider_DecisionModel_System_String_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [EvaluationDecisionModel](/api/E2E.Decision.Sdk/EvaluationDecisionModel/)
