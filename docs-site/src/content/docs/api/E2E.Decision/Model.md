---
title: "Model"
description: "Upstream's model.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Model"
  hidden: true
---

<a id="E2E_Decision_Model"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Model
```


Upstream's `model.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Model.cs)

## Methods

<a id="E2E_Decision_Model_CheckModel_"></a>
<a id="E2E_Decision_Model_CheckModel_System_Object_E2E_Decision_ModelNeeds_"></a>

### CheckModel(object?, ModelNeeds)


```csharp
public static void CheckModel(object? model, ModelNeeds needs)
```


Upstream's `checkModel`. Rejects anything the decision call cannot decide with, at config load: a model id string, a language
model, or a decision model that cannot answer the caller's question type.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
| `needs` | [ModelNeeds](/api/E2E.Decision/ModelNeeds/) |  |

<a id="E2E_Decision_Model_CheckProviderOptions_"></a>
<a id="E2E_Decision_Model_CheckProviderOptions_System_Collections_Generic_IReadOnlyDictionary_System_String_System_Text_Json_JsonElement__System_String_"></a>

### `CheckProviderOptions(IReadOnlyDictionary<string, JsonElement>?, string)`


```csharp
public static void CheckProviderOptions(IReadOnlyDictionary<string, JsonElement>? value, string caller)
```


Upstream's `checkProviderOptions`. Rejects `providerOptions` other than provider names mapped to option objects, the shape
the decision call takes, at config load.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `caller` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
