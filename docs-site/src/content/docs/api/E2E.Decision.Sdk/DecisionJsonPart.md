---
title: "DecisionJsonPart"
description: "A JSON value the model reads as data."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionJsonPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionJsonPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionJsonPart : DecisionStatePart, IEquatable<DecisionStatePart>, IEquatable<DecisionJsonPart>
```


A JSON value the model reads as data.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [DecisionStatePart](/api/E2E.Decision.Sdk/DecisionStatePart/)

**Implements:** [IEquatable&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionJsonPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionJsonPart__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionJsonPart__ctor_System_Text_Json_JsonElement_"></a>

### DecisionJsonPart(JsonElement)


```csharp
public DecisionJsonPart(JsonElement Value)
```


A JSON value the model reads as data.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionJsonPart_Value_"></a>
<a id="E2E_Decision_Sdk_DecisionJsonPart_Value"></a>

### Value


```csharp
public JsonElement Value { get; init; }
```


**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)
