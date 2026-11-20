---
title: "TypeSafeAiModelCatalog"
description: "The AI SDK's typesafe-ai-model-catalog.ts: the decision models TypeSafe lists and the questions they answer."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeSafeAiModelCatalog"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public static class TypeSafeAiModelCatalog
```


The AI SDK's `typesafe-ai-model-catalog.ts`: the decision models TypeSafe lists and the questions they answer.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiModelCatalog.cs)

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_ModelCatalog_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_ModelCatalog"></a>

### ModelCatalog


```csharp
public static IReadOnlyDictionary<string, ModelEntry> ModelCatalog { get; }
```


Upstream's `typeSafeAiModelCatalog`: one entry per id of [TypeSafeAiDecisionModelIds](#E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiDecisionModelIds).

**Value:** [IReadOnlyDictionary&lt;string, ModelEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiDecisionModelIds_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiDecisionModelIds"></a>

### TypeSafeAiDecisionModelIds


```csharp
public static IReadOnlyList<string> TypeSafeAiDecisionModelIds { get; }
```


Upstream's `typeSafeAiDecisionModelIds`. Any other string is accepted as a model id too.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiProviderOptions_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiProviderOptions"></a>

### TypeSafeAiProviderOptions


```csharp
public static ProviderOptionDefinitions TypeSafeAiProviderOptions { get; }
```


Upstream's `typeSafeAiProviderOptions`: the provider options the decision operation accepts.

**Value:** [ProviderOptionDefinitions](/api/E2E.Decision.Sdk/ProviderOptionDefinitions/)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiSupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_TypeSafeAiSupportedQuestionTypes"></a>

### TypeSafeAiSupportedQuestionTypes


```csharp
public static IReadOnlyList<string> TypeSafeAiSupportedQuestionTypes { get; }
```


Upstream's `typeSafeAiSupportedQuestionTypes`.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_GetTypeSafeAiDecisionModelEntry_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiModelCatalog_GetTypeSafeAiDecisionModelEntry_System_String_"></a>

### GetTypeSafeAiDecisionModelEntry(string)


```csharp
public static ModelEntry GetTypeSafeAiDecisionModelEntry(string modelId)
```


Upstream's `getTypeSafeAiDecisionModelEntry`. A model id outside the catalog reports long context as unknown.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelEntry](/api/E2E.Decision.Sdk/ModelEntry/)
