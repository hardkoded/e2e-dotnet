---
title: "NoSuchModelException"
description: "The AI SDK's NoSuchModelError: no model of that type has that id."
editUrl: false
prev: false
next: false
sidebar:
  label: "NoSuchModelException"
  hidden: true
---

<a id="E2E_Decision_Sdk_NoSuchModelException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public class NoSuchModelException : Exception, ISerializable
```


The AI SDK's `NoSuchModelError`: no model of that type has that id.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

**Derived:** [NoSuchProviderException](/api/E2E.Decision.Sdk/NoSuchProviderException/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_NoSuchModelException__ctor_"></a>
<a id="E2E_Decision_Sdk_NoSuchModelException__ctor_System_String_System_String_System_String_"></a>

### NoSuchModelException(string, string, string?)


```csharp
public NoSuchModelException(string modelId, string modelType, string? message = null)
```


The AI SDK's `NoSuchModelError`: no model of that type has that id.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_NoSuchModelException_ModelId_"></a>
<a id="E2E_Decision_Sdk_NoSuchModelException_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_NoSuchModelException_ModelType_"></a>
<a id="E2E_Decision_Sdk_NoSuchModelException_ModelType"></a>

### ModelType


```csharp
public string ModelType { get; }
```


The kind of model, such as `decisionModel`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
