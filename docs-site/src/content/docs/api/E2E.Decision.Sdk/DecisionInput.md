---
title: "DecisionInput"
description: "An instruction or a criterion description (the AI SDK's DecisionModelV4Input): text, or a JSON value the provider serializes into the prompt. A string conver..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionInput"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionInput"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionInput
```


An instruction or a criterion description (the AI SDK's `DecisionModelV4Input`): text, or a JSON value the provider
serializes into the prompt. A string converts implicitly.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionInput_Json_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_Json"></a>

### Json


```csharp
public JsonElement? Json { get; }
```


The value, when the input is JSON.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionInput_Null_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_Null"></a>

### Null


```csharp
public static DecisionInput Null { get; }
```


An explicit JSON `null`, as upstream's `instructions: null` or `criteria: { false: null }` reads. A C# `null` leaves the
field out. .NET-only: JavaScript tells an absent field from a `null` one, C# needs a value for the second.

**Value:** [DecisionInput](./)

<a id="E2E_Decision_Sdk_DecisionInput_Text_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_Text"></a>

### Text


```csharp
public string? Text { get; }
```


The text, when the input is a string.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Sdk_DecisionInput_FromJson_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_FromJson_System_Text_Json_JsonElement_"></a>

### FromJson(JsonElement)


```csharp
public static DecisionInput FromJson(JsonElement value)
```


A structured (object, array, number, boolean, or null) input.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |

**Returns:** [DecisionInput](./)

<a id="E2E_Decision_Sdk_DecisionInput_ToString_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_ToString"></a>

### ToString()


```csharp
public override string ToString()
```


Returns a string that represents the current object.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — A string that represents the current object.

<a id="E2E_Decision_Sdk_DecisionInput_ToText_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_ToText"></a>

### ToText()


```csharp
public string ToText()
```


The text a provider puts in its prompt: a string as is, JSON compact (`JSON.stringify`).

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

<a id="E2E_Decision_Sdk_DecisionInput_op_Implicit_"></a>
<a id="E2E_Decision_Sdk_DecisionInput_op_Implicit_System_String__E2E_Decision_Sdk_DecisionInput"></a>

### implicit operator DecisionInput?(string?)


```csharp
public static implicit operator DecisionInput?(string? text)
```


Converts a string to an input; null stays null.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionInput](./)
