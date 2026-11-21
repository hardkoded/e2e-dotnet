---
title: "CustomProviderOptions"
description: "The decision members of the arguments of the AI SDK's customProvider (ai/src/registry/custom-provider.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "CustomProviderOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_CustomProviderOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class CustomProviderOptions
```


The decision members of the arguments of the AI SDK's `customProvider` (`ai/src/registry/custom-provider.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/CustomProvider.cs)

## Properties

<a id="E2E_Decision_Sdk_CustomProviderOptions_DecisionModels_"></a>
<a id="E2E_Decision_Sdk_CustomProviderOptions_DecisionModels"></a>

### DecisionModels


```csharp
public IReadOnlyDictionary<string, IDecisionModel>? DecisionModels { get; init; }
```


The decision models, by id.

**Value:** [IReadOnlyDictionary&lt;string, IDecisionModel&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_CustomProviderOptions_EvaluationModels_"></a>
<a id="E2E_Decision_Sdk_CustomProviderOptions_EvaluationModels"></a>

### EvaluationModels


```csharp
public IReadOnlyDictionary<string, IEvaluationModel>? EvaluationModels { get; init; }
```


Deprecated: use [DecisionModels](#E2E_Decision_Sdk_CustomProviderOptions_DecisionModels). When both are set, [DecisionModels](#E2E_Decision_Sdk_CustomProviderOptions_DecisionModels) takes precedence.

**Value:** [IReadOnlyDictionary&lt;string, IEvaluationModel&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_CustomProviderOptions_FallbackProvider_"></a>
<a id="E2E_Decision_Sdk_CustomProviderOptions_FallbackProvider"></a>

### FallbackProvider


```csharp
public IDecisionProvider? FallbackProvider { get; init; }
```


The provider that answers an id the maps do not have.

**Value:** [IDecisionProvider](/api/E2E.Decision.Sdk/IDecisionProvider/)
