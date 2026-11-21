---
title: "DecisionRefusalException"
description: "The AI SDK's Experimental_DecisionRefusalError: the model refused to answer some questions."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionRefusalException"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionRefusalException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionRefusalException : Exception, ISerializable
```


The AI SDK's `Experimental_DecisionRefusalError`: the model refused to answer some questions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionRefusalException__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionRefusalException__ctor_System_Collections_Generic_IReadOnlyList_System_String__System_String_System_String_"></a>

### `DecisionRefusalException(IReadOnlyList<string>, string, string)`


```csharp
public DecisionRefusalException(IReadOnlyList<string> questionIds, string provider, string modelId)
```


The AI SDK's `Experimental_DecisionRefusalError`: the model refused to answer some questions.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `questionIds` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionRefusalException_QuestionIds_"></a>
<a id="E2E_Decision_Sdk_DecisionRefusalException_QuestionIds"></a>

### QuestionIds


```csharp
public IReadOnlyList<string> QuestionIds { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
