---
title: "JsonParseException"
description: "The AI SDK's JSONParseError: text that is not valid JSON."
editUrl: false
prev: false
next: false
sidebar:
  label: "JsonParseException"
  hidden: true
---

<a id="E2E_Decision_Sdk_JsonParseException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class JsonParseException : Exception, ISerializable
```


The AI SDK's `JSONParseError`: text that is not valid JSON.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_JsonParseException__ctor_"></a>
<a id="E2E_Decision_Sdk_JsonParseException__ctor_System_String_System_Exception_"></a>

### JsonParseException(string, Exception)


```csharp
public JsonParseException(string text, Exception cause)
```


The AI SDK's `JSONParseError`: text that is not valid JSON.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

## Properties

<a id="E2E_Decision_Sdk_JsonParseException_Text_"></a>
<a id="E2E_Decision_Sdk_JsonParseException_Text"></a>

### Text


```csharp
public string Text { get; }
```


The text that failed to parse.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
