---
title: "TestException"
description: "A locator, expectation, or test failed."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestException"
  hidden: true
---

<a id="E2E_TestException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TestException : E2EException, ISerializable
```


A locator, expectation, or test failed.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [E2EException](/api/E2E/E2EException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L62)

## Constructors

<a id="E2E_TestException__ctor_"></a>
<a id="E2E_TestException__ctor_System_String_System_String_"></a>

### TestException(string, string)


```csharp
public TestException(string code, string message)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L64)

<a id="E2E_TestException__ctor_System_String_System_String_System_Exception_"></a>

### TestException(string, string, Exception)


```csharp
public TestException(string code, string message, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L69)
