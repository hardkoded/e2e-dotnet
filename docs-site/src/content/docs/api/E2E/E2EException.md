---
title: "E2EException"
description: "Base exception for the agent and engines. Code is stable."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2EException"
  hidden: true
---

<a id="E2E_E2EException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class E2EException : Exception, ISerializable
```


Base exception for the agent and engines. [Code](#E2E_E2EException_Code) is stable.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

**Derived:** [AgentException](/api/E2E/AgentException/), [ConfigurationException](/api/E2E/ConfigurationException/), [EngineException](/api/E2E/EngineException/), [OAuthException](/api/E2E.OAuth/OAuthException/), [SkipException](/api/E2E/SkipException/), [TestException](/api/E2E/TestException/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L8)

## Constructors

<a id="E2E_E2EException__ctor_"></a>
<a id="E2E_E2EException__ctor_System_String_System_String_"></a>

### E2EException(string, string)


```csharp
public E2EException(string code, string message)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L10)

<a id="E2E_E2EException__ctor_System_String_System_String_System_Exception_"></a>

### E2EException(string, string, Exception)


```csharp
public E2EException(string code, string message, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L16)

## Properties

<a id="E2E_E2EException_Code_"></a>
<a id="E2E_E2EException_Code"></a>

### Code


```csharp
public string Code { get; }
```


Stable machine-readable code, such as `ASSERTION_FAILED`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L23)

<a id="E2E_E2EException_Details_"></a>
<a id="E2E_E2EException_Details"></a>

### Details


```csharp
public ErrorDetails? Details { get; init; }
```


Structured facts of the failure beside its message, when the thrower had them. See [ErrorDetails](/api/E2E/ErrorDetails/).

**Value:** [ErrorDetails](/api/E2E/ErrorDetails/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L26)
