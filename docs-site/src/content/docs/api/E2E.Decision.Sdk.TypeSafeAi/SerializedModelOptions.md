---
title: "SerializedModelOptions"
description: "What crosses a workflow boundary for a model (the object upstream's serializeModelOptions returns)."
editUrl: false
prev: false
next: false
sidebar:
  label: "SerializedModelOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed record SerializedModelOptions : IEquatable<SerializedModelOptions>
```


What crosses a workflow boundary for a model (the object upstream's `serializeModelOptions` returns).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SerializedModelOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiDecisionModel.cs)

## Constructors

<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions__ctor_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions__ctor_System_String_System_Text_Json_Nodes_JsonObject_"></a>

### SerializedModelOptions(string, JsonObject)


```csharp
public SerializedModelOptions(string ModelId, JsonObject Config)
```


What crosses a workflow boundary for a model (the object upstream's `serializeModelOptions` returns).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ModelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The model id. |
| `Config` | [JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject) | The JSON-serializable config: `provider`, `baseURL`, and the resolved `headers` when there are any. |

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions_Config_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions_Config"></a>

### Config


```csharp
public JsonObject Config { get; init; }
```


The JSON-serializable config: `provider`, `baseURL`, and the resolved `headers` when there are any.

**Value:** [JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject)

<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions_ModelId_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; init; }
```


The model id.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
