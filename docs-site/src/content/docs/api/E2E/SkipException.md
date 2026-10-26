---
title: "SkipException"
description: "Thrown by String) when a test stops early and is reported skipped."
editUrl: false
prev: false
next: false
sidebar:
  label: "SkipException"
  hidden: true
---

<a id="E2E_SkipException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SkipException : E2EException, ISerializable
```


Thrown by `String)` when a test stops early and is reported skipped.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [E2EException](/api/E2E/E2EException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L201)

## Constructors

<a id="E2E_SkipException__ctor_"></a>
<a id="E2E_SkipException__ctor_System_String_"></a>

### SkipException(string)


```csharp
public SkipException(string reason)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Errors/E2EException.cs#L203)
