---
title: "ConfigurationException"
description: "e2e.config.json is invalid. The code is INVALIDCONFIG."
editUrl: false
prev: false
next: false
sidebar:
  label: "ConfigurationException"
  hidden: true
---

<a id="E2E_ConfigurationException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ConfigurationException : E2EException, ISerializable
```


`e2e.config.json` is invalid. The code is `INVALID_CONFIG`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [E2EException](/api/E2E/E2EException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L210)

## Constructors

<a id="E2E_ConfigurationException__ctor_"></a>
<a id="E2E_ConfigurationException__ctor_System_String_System_String_"></a>

### ConfigurationException(string, string)


```csharp
public ConfigurationException(string code, string message)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L212)

<a id="E2E_ConfigurationException__ctor_System_String_System_String_System_Exception_"></a>

### ConfigurationException(string, string, Exception)


```csharp
public ConfigurationException(string code, string message, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L217)
