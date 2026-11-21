---
title: "InvalidResponseDataException"
description: "The AI SDK's InvalidResponseDataError: the provider's answers break the contract of the questions."
editUrl: false
prev: false
next: false
sidebar:
  label: "InvalidResponseDataException"
  hidden: true
---

<a id="E2E_Decision_Sdk_InvalidResponseDataException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class InvalidResponseDataException : Exception, ISerializable
```


The AI SDK's `InvalidResponseDataError`: the provider's answers break the contract of the questions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_InvalidResponseDataException__ctor_"></a>
<a id="E2E_Decision_Sdk_InvalidResponseDataException__ctor_System_String_System_Object_"></a>

### InvalidResponseDataException(string, object?)


```csharp
public InvalidResponseDataException(string message, object? answers = null)
```


The AI SDK's `InvalidResponseDataError`: the provider's answers break the contract of the questions.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `answers` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

## Properties

<a id="E2E_Decision_Sdk_InvalidResponseDataException_Answers_"></a>
<a id="E2E_Decision_Sdk_InvalidResponseDataException_Answers"></a>

### Answers


```csharp
public object? Answers { get; }
```


The rejected answers.

**Value:** [object](https://learn.microsoft.com/dotnet/api/system.object)
