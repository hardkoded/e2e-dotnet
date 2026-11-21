---
title: "InvalidArgumentException"
description: "The AI SDK's InvalidArgumentError: a call argument the SDK rejects before any request."
editUrl: false
prev: false
next: false
sidebar:
  label: "InvalidArgumentException"
  hidden: true
---

<a id="E2E_Decision_Sdk_InvalidArgumentException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class InvalidArgumentException : Exception, ISerializable
```


The AI SDK's `InvalidArgumentError`: a call argument the SDK rejects before any request.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_InvalidArgumentException__ctor_"></a>
<a id="E2E_Decision_Sdk_InvalidArgumentException__ctor_System_String_System_String_"></a>

### InvalidArgumentException(string, string)


```csharp
public InvalidArgumentException(string parameter, string message)
```


The AI SDK's `InvalidArgumentError`: a call argument the SDK rejects before any request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `parameter` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_InvalidArgumentException_Parameter_"></a>
<a id="E2E_Decision_Sdk_InvalidArgumentException_Parameter"></a>

### Parameter


```csharp
public string Parameter { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
