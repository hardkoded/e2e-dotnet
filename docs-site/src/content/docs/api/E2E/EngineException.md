---
title: "EngineException"
description: "The engine refused an operation or could not drive the app."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineException"
  hidden: true
---

<a id="E2E_EngineException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class EngineException : E2EException, ISerializable
```


The engine refused an operation or could not drive the app.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [E2EException](/api/E2E/E2EException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L132)

## Constructors

<a id="E2E_EngineException__ctor_"></a>
<a id="E2E_EngineException__ctor_System_String_System_String_"></a>

### EngineException(string, string)


```csharp
public EngineException(string code, string message)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L134)

<a id="E2E_EngineException__ctor_System_String_System_String_System_Boolean_System_Exception_"></a>

### EngineException(string, string, bool, Exception?)


```csharp
public EngineException(string code, string message, bool retryable, Exception? inner = null)
```


Creates an engine error that may claim to be retryable. Only [NodeStale](/api/E2E/EngineErrorCodes/#E2E_EngineErrorCodes_NodeStale) and
[FrameNotFound](/api/E2E/EngineErrorCodes/#E2E_EngineErrorCodes_FrameNotFound) can be retried. Any other retryable claim becomes a
non-retryable [EngineFailure](/api/E2E/EngineErrorCodes/#E2E_EngineErrorCodes_EngineFailure), so an engine can never ask for an action that
may have committed to run again.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `retryable` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L150)

<a id="E2E_EngineException__ctor_System_String_System_String_System_Exception_"></a>

### EngineException(string, string, Exception)


```csharp
public EngineException(string code, string message, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L139)

## Properties

<a id="E2E_EngineException_Retryable_"></a>
<a id="E2E_EngineException_Retryable"></a>

### Retryable


```csharp
public bool Retryable { get; }
```


Whether the caller may observe again and repeat the operation.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L157)
