---
title: "DecisionUnsupportedQuestionTypeException"
description: "The AI SDK's Experimental_DecisionUnsupportedQuestionTypeError: the model cannot answer a question's type."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionUnsupportedQuestionTypeException"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionUnsupportedQuestionTypeException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionUnsupportedQuestionTypeException : Exception, ISerializable
```


The AI SDK's `Experimental_DecisionUnsupportedQuestionTypeError`: the model cannot answer a question's type.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionUnsupportedQuestionTypeException__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionUnsupportedQuestionTypeException__ctor_System_String_System_String_System_String_System_String_"></a>

### DecisionUnsupportedQuestionTypeException(string, string, string, string)


```csharp
public DecisionUnsupportedQuestionTypeException(string questionId, string questionType, string provider, string modelId)
```


The AI SDK's `Experimental_DecisionUnsupportedQuestionTypeError`: the model cannot answer a question's type.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `questionId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `questionType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionUnsupportedQuestionTypeException_QuestionId_"></a>
<a id="E2E_Decision_Sdk_DecisionUnsupportedQuestionTypeException_QuestionId"></a>

### QuestionId


```csharp
public string QuestionId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
