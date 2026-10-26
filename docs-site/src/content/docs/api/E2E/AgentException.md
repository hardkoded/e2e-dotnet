---
title: "AgentException"
description: "An agent step failed, was blocked, or could not talk to its model."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentException"
  hidden: true
---

<a id="E2E_AgentException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AgentException : E2EException, ISerializable
```


An agent step failed, was blocked, or could not talk to its model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [E2EException](/api/E2E/E2EException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L76)

## Constructors

<a id="E2E_AgentException__ctor_"></a>
<a id="E2E_AgentException__ctor_System_String_System_String_"></a>

### AgentException(string, string)


```csharp
public AgentException(string code, string message)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L78)

<a id="E2E_AgentException__ctor_System_String_System_String_System_Boolean_"></a>

### AgentException(string, string, bool)


```csharp
public AgentException(string code, string message, bool blocked)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `blocked` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L88)

<a id="E2E_AgentException__ctor_System_String_System_String_System_Boolean_System_Exception_"></a>

### AgentException(string, string, bool, Exception)


```csharp
public AgentException(string code, string message, bool blocked, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `blocked` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L94)

<a id="E2E_AgentException__ctor_System_String_System_String_System_Exception_"></a>

### AgentException(string, string, Exception)


```csharp
public AgentException(string code, string message, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L83)

## Properties

<a id="E2E_AgentException_Blocked_"></a>
<a id="E2E_AgentException_Blocked"></a>

### Blocked


```csharp
public bool Blocked { get; }
```


True when credentials, the environment, test setup, or the agent's own budget stopped the step,
rather than the app misbehaving.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L128)

<a id="E2E_AgentException_Explanation_"></a>
<a id="E2E_AgentException_Explanation"></a>

### Explanation


```csharp
public string Explanation { get; }
```


The step's account of what happened. The same text as [Message](https://learn.microsoft.com/dotnet/api/system.exception.message).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L101)

<a id="E2E_AgentException_Screenshot_"></a>
<a id="E2E_AgentException_Screenshot"></a>

### Screenshot


```csharp
public string? Screenshot { get; init; }
```


Where a failed `AssertAsync` kept its redacted screenshot, relative to the project root. Null when none was taken:
`Screenshot = false` or the engine captures no pixels.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L110)
