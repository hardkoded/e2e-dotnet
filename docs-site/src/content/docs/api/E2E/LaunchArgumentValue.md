---
title: "LaunchArgumentValue"
description: "One value in the object form of app.launchArguments (upstream's LaunchArgumentValue, string | number | boolean | string[]). On iOS a value is spelled as NSUs..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LaunchArgumentValue"
  hidden: true
---

<a id="E2E_LaunchArgumentValue"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public readonly struct LaunchArgumentValue : IEquatable<LaunchArgumentValue>
```


One value in the object form of `app.launchArguments` (upstream's `LaunchArgumentValue`, `string | number | boolean | string[]`). On iOS
a value is spelled as `NSUserDefaults` reads it: `YES` or `NO` for a boolean, a plist array for a list. On Android each value
is the `am start` extra of its type (`--ez` boolean, `--ei` or `--el` integer, `--ef` float, `--es` string, `--esa` list).
.NET-only holder: C# has no union type, so the four forms are one struct with an implicit conversion from each.

**Implements:** [IEquatable&lt;LaunchArgumentValue&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/LaunchArgumentValue.cs)

## Properties

<a id="E2E_LaunchArgumentValue_Flag_"></a>
<a id="E2E_LaunchArgumentValue_Flag"></a>

### Flag


```csharp
public bool? Flag { get; }
```


The boolean, when [Kind](#E2E_LaunchArgumentValue_Kind) is [Boolean](/api/E2E/LaunchArgumentValueKind/#E2E_LaunchArgumentValueKind_Boolean).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_LaunchArgumentValue_Items_"></a>
<a id="E2E_LaunchArgumentValue_Items"></a>

### Items


```csharp
public IReadOnlyList<string>? Items { get; }
```


The items, when [Kind](#E2E_LaunchArgumentValue_Kind) is [List](/api/E2E/LaunchArgumentValueKind/#E2E_LaunchArgumentValueKind_List).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_LaunchArgumentValue_Kind_"></a>
<a id="E2E_LaunchArgumentValue_Kind"></a>

### Kind


```csharp
public LaunchArgumentValueKind Kind { get; }
```


Which form this value is.

**Value:** [LaunchArgumentValueKind](/api/E2E/LaunchArgumentValueKind/)

<a id="E2E_LaunchArgumentValue_Number_"></a>
<a id="E2E_LaunchArgumentValue_Number"></a>

### Number


```csharp
public double? Number { get; }
```


The number, when [Kind](#E2E_LaunchArgumentValue_Kind) is [Number](/api/E2E/LaunchArgumentValueKind/#E2E_LaunchArgumentValueKind_Number).

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_LaunchArgumentValue_Text_"></a>
<a id="E2E_LaunchArgumentValue_Text"></a>

### Text


```csharp
public string? Text { get; }
```


The string, when [Kind](#E2E_LaunchArgumentValue_Kind) is [String](/api/E2E/LaunchArgumentValueKind/#E2E_LaunchArgumentValueKind_String).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_LaunchArgumentValue_Equals_"></a>
<a id="E2E_LaunchArgumentValue_Equals_E2E_LaunchArgumentValue_"></a>

### Equals(LaunchArgumentValue)


```csharp
public bool Equals(LaunchArgumentValue other)
```


Indicates whether the current object is equal to another object of the same type.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `other` | [LaunchArgumentValue](./) | An object to compare with this object. |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean) — &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool"&gt;true&lt;/a&gt; if the current object is equal to the &lt;code class="paramref"&gt;other&lt;/code&gt; parameter; otherwise, &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool"&gt;false&lt;/a&gt;.

<a id="E2E_LaunchArgumentValue_Equals_System_Object_"></a>

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

<a id="E2E_LaunchArgumentValue_FromList_"></a>
<a id="E2E_LaunchArgumentValue_FromList_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `FromList(IReadOnlyList<string>)`


```csharp
public static LaunchArgumentValue FromList(IReadOnlyList<string> items)
```


A list value from any read-only list.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `items` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_GetHashCode_"></a>
<a id="E2E_LaunchArgumentValue_GetHashCode"></a>

### GetHashCode()


```csharp
public override int GetHashCode()
```


Returns the hash code for this instance.

**Returns:** [int](https://learn.microsoft.com/dotnet/api/system.int32) — A 32-bit signed integer that is the hash code for this instance.

<a id="E2E_LaunchArgumentValue_ToString_"></a>
<a id="E2E_LaunchArgumentValue_ToString"></a>

### ToString()


```csharp
public override string ToString()
```


The value as JavaScript's `String(value)` spells it: the text, `true` or `false`, the number, or the items joined by commas.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

<a id="E2E_LaunchArgumentValue_op_Equality_"></a>
<a id="E2E_LaunchArgumentValue_op_Equality_E2E_LaunchArgumentValue_E2E_LaunchArgumentValue_"></a>

### operator ==(LaunchArgumentValue, LaunchArgumentValue)


```csharp
public static bool operator ==(LaunchArgumentValue left, LaunchArgumentValue right)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `left` | [LaunchArgumentValue](./) |  |
| `right` | [LaunchArgumentValue](./) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_LaunchArgumentValue_op_Implicit_"></a>
<a id="E2E_LaunchArgumentValue_op_Implicit_System_Boolean__E2E_LaunchArgumentValue"></a>

### implicit operator LaunchArgumentValue(bool)


```csharp
public static implicit operator LaunchArgumentValue(bool value)
```


A boolean value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Implicit_System_Collections_Generic_List_System_String___E2E_LaunchArgumentValue"></a>

### `implicit operator LaunchArgumentValue(List<string>)`


```csharp
public static implicit operator LaunchArgumentValue(List<string> value)
```


A list value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Implicit_System_Double__E2E_LaunchArgumentValue"></a>

### implicit operator LaunchArgumentValue(double)


```csharp
public static implicit operator LaunchArgumentValue(double value)
```


A number value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Implicit_System_Int32__E2E_LaunchArgumentValue"></a>

### implicit operator LaunchArgumentValue(int)


```csharp
public static implicit operator LaunchArgumentValue(int value)
```


A number value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Implicit_System_Int64__E2E_LaunchArgumentValue"></a>

### implicit operator LaunchArgumentValue(long)


```csharp
public static implicit operator LaunchArgumentValue(long value)
```


A number value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Implicit_System_String__E2E_LaunchArgumentValue"></a>

### implicit operator LaunchArgumentValue(string)


```csharp
public static implicit operator LaunchArgumentValue(string value)
```


A string value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Implicit_System_String____E2E_LaunchArgumentValue"></a>

### implicit operator LaunchArgumentValue(string[])


```csharp
public static implicit operator LaunchArgumentValue(string[] value)
```


A list value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string\[\]](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [LaunchArgumentValue](./)

<a id="E2E_LaunchArgumentValue_op_Inequality_"></a>
<a id="E2E_LaunchArgumentValue_op_Inequality_E2E_LaunchArgumentValue_E2E_LaunchArgumentValue_"></a>

### operator !=(LaunchArgumentValue, LaunchArgumentValue)


```csharp
public static bool operator !=(LaunchArgumentValue left, LaunchArgumentValue right)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `left` | [LaunchArgumentValue](./) |  |
| `right` | [LaunchArgumentValue](./) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
