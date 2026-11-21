---
title: "TypeSafeAiProvider"
description: "The AI SDK's createTypeSafeAi and the typeSafeAi provider (typesafe-ai-provider.ts), for TypeSafe's experimental decision capability: typeSafeAi.decisionMode..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeSafeAiProvider"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed class TypeSafeAiProvider
```


The AI SDK's `createTypeSafeAi` and the `typeSafeAi` provider (`typesafe-ai-provider.ts`), for TypeSafe's experimental
decision capability: `typeSafeAi.decisionModel('jev-latest')`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiProvider.cs)

## Constructors

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider__ctor_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider__ctor_E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProviderSettings_"></a>

### TypeSafeAiProvider(TypeSafeAiProviderSettings?)


```csharp
public TypeSafeAiProvider(TypeSafeAiProviderSettings? settings = null)
```


Upstream's `createTypeSafeAi`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `settings` | [TypeSafeAiProviderSettings](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiProviderSettings/) |  |

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_SpecificationVersion"></a>

### SpecificationVersion


```csharp
public string SpecificationVersion { get; }
```


Upstream's `specificationVersion`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_TypeSafeAi_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_TypeSafeAi"></a>

### TypeSafeAi


```csharp
public static TypeSafeAiProvider TypeSafeAi { get; }
```


Upstream's `typeSafeAi`: the provider the environment configures.

**Value:** [TypeSafeAiProvider](./)

## Methods

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_DecisionModel_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_DecisionModel_System_String_"></a>

### DecisionModel(string)


```csharp
public DecisionTypeSafeAiModel DecisionModel(string modelId)
```


Creates a decision model (upstream's `decisionModel`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionTypeSafeAiModel](/api/E2E.Decision.Sdk.TypeSafeAi/DecisionTypeSafeAiModel/)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_EmbeddingModel_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_EmbeddingModel_System_String_"></a>

### EmbeddingModel(string)


```csharp
public void EmbeddingModel(string modelId)
```


Upstream's `embeddingModel`: TypeSafe has none, so it throws [NoSuchModelException](/api/E2E.Decision.Sdk/NoSuchModelException/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_EvaluationModel_System_String_"></a>

### EvaluationModel(string)


```csharp
public DecisionTypeSafeAiModel EvaluationModel(string modelId)
```


Deprecated: use [DecisionModel(string)](#E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_DecisionModel_System_String_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionTypeSafeAiModel](/api/E2E.Decision.Sdk.TypeSafeAi/DecisionTypeSafeAiModel/)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_ImageModel_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_ImageModel_System_String_"></a>

### ImageModel(string)


```csharp
public void ImageModel(string modelId)
```


Upstream's `imageModel`: TypeSafe has none, so it throws [NoSuchModelException](/api/E2E.Decision.Sdk/NoSuchModelException/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_LanguageModel_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiProvider_LanguageModel_System_String_"></a>

### LanguageModel(string)


```csharp
public void LanguageModel(string modelId)
```


Upstream's `languageModel`: TypeSafe has none, so it throws [NoSuchModelException](/api/E2E.Decision.Sdk/NoSuchModelException/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
