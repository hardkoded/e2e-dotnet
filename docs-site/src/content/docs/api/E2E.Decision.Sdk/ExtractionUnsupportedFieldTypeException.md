---
title: "ExtractionUnsupportedFieldTypeException"
description: "The AI SDK's Experimental_ExtractionUnsupportedFieldTypeError: the extraction model does not support the type of a requested field."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExtractionUnsupportedFieldTypeException"
  hidden: true
---

<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class ExtractionUnsupportedFieldTypeException : Exception, ISerializable
```


The AI SDK's `Experimental_ExtractionUnsupportedFieldTypeError`: the extraction model does not support the type of a requested field.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException__ctor_"></a>
<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException__ctor_System_String_System_String_System_String_System_String_System_String_"></a>

### ExtractionUnsupportedFieldTypeException(string, string, string, string, string?)


```csharp
public ExtractionUnsupportedFieldTypeException(string fieldId, string fieldType, string provider, string modelId, string? message = null)
```


The AI SDK's `Experimental_ExtractionUnsupportedFieldTypeError`: the extraction model does not support the type of a requested field.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `fieldId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `fieldType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_FieldId_"></a>
<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_FieldId"></a>

### FieldId


```csharp
public string FieldId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_FieldType_"></a>
<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_FieldType"></a>

### FieldType


```csharp
public string FieldType { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_ModelId_"></a>
<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_Provider_"></a>
<a id="E2E_Decision_Sdk_ExtractionUnsupportedFieldTypeException_Provider"></a>

### Provider


```csharp
public string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
