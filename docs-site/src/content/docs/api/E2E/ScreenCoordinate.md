---
title: "ScreenCoordinate"
description: "Upstream's ScreenCoordinate: CSS pixels, or a percentage of the viewport's width (x) or height (y), '0%' through '100%'. A double converts to pixels and a st..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScreenCoordinate"
  hidden: true
---

<a id="E2E_ScreenCoordinate"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public readonly struct ScreenCoordinate : IEquatable<ScreenCoordinate>
```


Upstream's `ScreenCoordinate`: CSS pixels, or a percentage of the viewport's width (x) or height (y), `'0%'` through
`'100%'`. A [double](https://learn.microsoft.com/dotnet/api/system.double) converts to pixels and a [string](https://learn.microsoft.com/dotnet/api/system.string) such as `"50%"` to a percentage, which is checked
when the call runs, as upstream does.

**Implements:** [IEquatable&lt;ScreenCoordinate&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs)

## Methods

<a id="E2E_ScreenCoordinate_Equals_"></a>
<a id="E2E_ScreenCoordinate_Equals_E2E_ScreenCoordinate_"></a>

### Equals(ScreenCoordinate)


```csharp
public bool Equals(ScreenCoordinate other)
```


Indicates whether the current object is equal to another object of the same type.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `other` | [ScreenCoordinate](./) | An object to compare with this object. |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean) — &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool"&gt;true&lt;/a&gt; if the current object is equal to the &lt;code class="paramref"&gt;other&lt;/code&gt; parameter; otherwise, &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool"&gt;false&lt;/a&gt;.

<a id="E2E_ScreenCoordinate_Equals_System_Object_"></a>

### Equals(object?)


```csharp
public override bool Equals(object? obj)
```


Indicates whether this instance and a specified object are equal.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `obj` | [object](https://learn.microsoft.com/dotnet/api/system.object) | The object to compare with the current instance. |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean) — &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool"&gt;true&lt;/a&gt; if &lt;code class="paramref"&gt;obj&lt;/code&gt; and this instance are the same type and represent the same value; otherwise, &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool"&gt;false&lt;/a&gt;.

<a id="E2E_ScreenCoordinate_GetHashCode_"></a>
<a id="E2E_ScreenCoordinate_GetHashCode"></a>

### GetHashCode()


```csharp
public override int GetHashCode()
```


Returns the hash code for this instance.

**Returns:** [int](https://learn.microsoft.com/dotnet/api/system.int32) — A 32-bit signed integer that is the hash code for this instance.

<a id="E2E_ScreenCoordinate_ToString_"></a>
<a id="E2E_ScreenCoordinate_ToString"></a>

### ToString()


```csharp
public override string ToString()
```


The coordinate as written: `12.5` or `50%`.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

<a id="E2E_ScreenCoordinate_op_Equality_"></a>
<a id="E2E_ScreenCoordinate_op_Equality_E2E_ScreenCoordinate_E2E_ScreenCoordinate_"></a>

### operator ==(ScreenCoordinate, ScreenCoordinate)


```csharp
public static bool operator ==(ScreenCoordinate left, ScreenCoordinate right)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `left` | [ScreenCoordinate](./) |  |
| `right` | [ScreenCoordinate](./) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_ScreenCoordinate_op_Implicit_"></a>
<a id="E2E_ScreenCoordinate_op_Implicit_System_Double__E2E_ScreenCoordinate"></a>

### implicit operator ScreenCoordinate(double)


```csharp
public static implicit operator ScreenCoordinate(double pixels)
```


CSS pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pixels` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [ScreenCoordinate](./)

<a id="E2E_ScreenCoordinate_op_Implicit_System_String__E2E_ScreenCoordinate"></a>

### implicit operator ScreenCoordinate(string)


```csharp
public static implicit operator ScreenCoordinate(string percentage)
```


A percentage of the viewport, `"50%"`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `percentage` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ScreenCoordinate](./)

<a id="E2E_ScreenCoordinate_op_Inequality_"></a>
<a id="E2E_ScreenCoordinate_op_Inequality_E2E_ScreenCoordinate_E2E_ScreenCoordinate_"></a>

### operator !=(ScreenCoordinate, ScreenCoordinate)


```csharp
public static bool operator !=(ScreenCoordinate left, ScreenCoordinate right)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `left` | [ScreenCoordinate](./) |  |
| `right` | [ScreenCoordinate](./) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
