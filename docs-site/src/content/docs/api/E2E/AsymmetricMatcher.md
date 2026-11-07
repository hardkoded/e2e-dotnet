---
title: "AsymmetricMatcher"
description: "A stand-in for a value inside ToEqual, ToMatchObject, ToContain, and ToHaveProperty: Any(Type) in place of a number, ObjectContaining(object) in place of a r..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AsymmetricMatcher"
  hidden: true
---

<a id="E2E_AsymmetricMatcher"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public abstract class AsymmetricMatcher
```


A stand-in for a value inside `ToEqual`, `ToMatchObject`, `ToContain`, and
`ToHaveProperty`: [Any(Type)](/api/E2E/Expect/#E2E_Expect_Any_System_Type_) in place of a number, [ObjectContaining(object)](/api/E2E/Expect/#E2E_Expect_ObjectContaining_System_Object_) in place of
a record. The structural matchers ask it whether a value matches instead of comparing.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/AsymmetricMatchers.cs)

## Methods

<a id="E2E_AsymmetricMatcher_AsymmetricMatch_"></a>
<a id="E2E_AsymmetricMatcher_AsymmetricMatch_System_Object_"></a>

### AsymmetricMatch(object?)


```csharp
public abstract bool AsymmetricMatch(object? other)
```


Whether &lt;code class="paramref"&gt;other&lt;/code&gt; satisfies the matcher.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `other` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_AsymmetricMatcher_ToString_"></a>
<a id="E2E_AsymmetricMatcher_ToString"></a>

### ToString()


```csharp
public override abstract string ToString()
```


The matcher's name, `ObjectContaining`; a failure message adds the sample.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
