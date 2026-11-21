---
title: "UnsupportedFunctionalityException"
description: "The AI SDK's UnsupportedFunctionalityError: the provider cannot take an input, such as a file of an unsupported media type."
editUrl: false
prev: false
next: false
sidebar:
  label: "UnsupportedFunctionalityException"
  hidden: true
---

<a id="E2E_Decision_Sdk_UnsupportedFunctionalityException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class UnsupportedFunctionalityException : Exception, ISerializable
```


The AI SDK's `UnsupportedFunctionalityError`: the provider cannot take an input, such as a file of an unsupported media type.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_UnsupportedFunctionalityException__ctor_"></a>
<a id="E2E_Decision_Sdk_UnsupportedFunctionalityException__ctor_System_String_"></a>

### UnsupportedFunctionalityException(string)


```csharp
public UnsupportedFunctionalityException(string functionality)
```


Keeps the one-argument signature of earlier versions.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `functionality` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_Decision_Sdk_UnsupportedFunctionalityException__ctor_System_String_System_String_"></a>

### UnsupportedFunctionalityException(string, string?)


```csharp
public UnsupportedFunctionalityException(string functionality, string? message = null)
```


The AI SDK's `UnsupportedFunctionalityError`: the provider cannot take an input, such as a file of an unsupported media type.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `functionality` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_UnsupportedFunctionalityException_Functionality_"></a>
<a id="E2E_Decision_Sdk_UnsupportedFunctionalityException_Functionality"></a>

### Functionality


```csharp
public string Functionality { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
