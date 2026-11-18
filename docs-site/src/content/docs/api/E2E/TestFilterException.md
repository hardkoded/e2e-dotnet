---
title: "TestFilterException"
description: "What a filter throws when it cannot finish but still knows how likely some tests are to fail, such as the ones the change touches. .NET-only: upstream reads..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestFilterException"
  hidden: true
---

<a id="E2E_TestFilterException"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class TestFilterException : Exception, ISerializable
```


What a filter throws when it cannot finish but still knows how likely some tests are to fail, such as the ones the change touches. .NET-only:
upstream reads a `relevance` property off whatever the filter throws, which C# has no way to name without a type.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs)

## Constructors

<a id="E2E_TestFilterException__ctor_"></a>
<a id="E2E_TestFilterException__ctor"></a>

### TestFilterException()


```csharp
public TestFilterException()
```


Creates the exception.

<a id="E2E_TestFilterException__ctor_System_String_"></a>

### TestFilterException(string)


```csharp
public TestFilterException(string message)
```


Creates the exception with a message.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_TestFilterException__ctor_System_String_System_Exception_"></a>

### TestFilterException(string, Exception)


```csharp
public TestFilterException(string message, Exception innerException)
```


Creates the exception with a message and the exception that caused it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `innerException` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

## Properties

<a id="E2E_TestFilterException_Relevance_"></a>
<a id="E2E_TestFilterException_Relevance"></a>

### Relevance


```csharp
public virtual IReadOnlyDictionary<string, double>? Relevance { get; init; }
```


Shaped like [Relevance](/api/E2E/TestFilterResult/#E2E_TestFilterResult_Relevance); the run checks it as it checks a result's. Null scores nothing.

**Value:** [IReadOnlyDictionary&lt;string, double&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
