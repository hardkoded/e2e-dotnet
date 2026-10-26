---
title: "UniqueValue"
description: "A value that changes every run, such as a timestamp or a fresh email. The replay cache treats every UniqueValue as the same param and substitutes the current..."
editUrl: false
prev: false
next: false
sidebar:
  label: "UniqueValue"
  hidden: true
---

<a id="E2E_UniqueValue"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class UniqueValue
```


A value that changes every run, such as a timestamp or a fresh email.
The replay cache treats every [UniqueValue](./) as the same param
and substitutes the current value when it types.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L70)

## Constructors

<a id="E2E_UniqueValue__ctor_"></a>
<a id="E2E_UniqueValue__ctor_System_String_"></a>

### UniqueValue(string)


```csharp
public UniqueValue(string value)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L72)

## Properties

<a id="E2E_UniqueValue_Value_"></a>
<a id="E2E_UniqueValue_Value"></a>

### Value


```csharp
public string Value { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L88)

## Methods

<a id="E2E_UniqueValue_ToString_"></a>
<a id="E2E_UniqueValue_ToString"></a>

### ToString()


```csharp
public override string ToString()
```


Returns a string that represents the current object.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — A string that represents the current object.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L90)
