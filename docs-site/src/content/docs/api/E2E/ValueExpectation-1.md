---
title: "ValueExpectation<T>"
description: "Synchronous matchers over a plain value, from That<T>. A failure is an ASSERTION_FAILED TestException. The optional message of the call opens every failure,..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ValueExpectation<T>"
  hidden: true
---

<a id="E2E_ValueExpectation_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ValueExpectation<T>
```


Synchronous matchers over a plain value, from `That&lt;T&gt;`. A failure is an `ASSERTION_FAILED`
[TestException](/api/E2E/TestException/). The optional message of the call opens every failure, and survives `Not`.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

.NET has no `undefined`: [Undefined](/api/E2E/Expect/#E2E_Expect_Undefined) stands for it, and `null` is only `null`. Values compare by structure whatever
their class: see `ToEqual`. There is no Standard Schema in .NET: `ToMatchSchema&lt;T&gt;` takes a
`StandardSchemaV1&lt;T&gt;` instead, a delegate that any schema library can be adapted to.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Values.cs)

## Properties

<a id="E2E_ValueExpectation_1_Not_"></a>
<a id="E2E_ValueExpectation_1_Not"></a>

### Not


```csharp
public ValueExpectation<T> Not { get; }
```


Inverts the matcher.

**Value:** [ValueExpectation&lt;T&gt;](./)

## Methods

<a id="E2E_ValueExpectation_1_ToBe_"></a>
<a id="E2E_ValueExpectation_1_ToBe__0_"></a>

### ToBe(T)


```csharp
public void ToBe(T expected)
```


Compares with JavaScript's `Object.is`: a number equals itself (`NaN` included), `0` is not `-0`, and an object must be the same instance.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | `T` |  |

<a id="E2E_ValueExpectation_1_ToBeCloseTo_"></a>
<a id="E2E_ValueExpectation_1_ToBeCloseTo_System_Double_System_Int32_"></a>

### ToBeCloseTo(double, int)


```csharp
public void ToBeCloseTo(double expected, int digits = 2)
```


Jest's rule: the difference must be under half a unit of the last kept digit, `10 ** -digits / 2`, so `ToBeCloseTo(60)`
accepts 59.996 and rejects 59.99. Infinite values pass only when they are the same infinity.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `digits` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

<a id="E2E_ValueExpectation_1_ToBeDefined_"></a>
<a id="E2E_ValueExpectation_1_ToBeDefined"></a>

### ToBeDefined()


```csharp
public void ToBeDefined()
```


Requires any value but [Undefined](/api/E2E/Expect/#E2E_Expect_Undefined): `null` is defined.

<a id="E2E_ValueExpectation_1_ToBeFalsy_"></a>
<a id="E2E_ValueExpectation_1_ToBeFalsy"></a>

### ToBeFalsy()


```csharp
public void ToBeFalsy()
```


Requires a falsy value.

<a id="E2E_ValueExpectation_1_ToBeGreaterThan_"></a>
<a id="E2E_ValueExpectation_1_ToBeGreaterThan_System_Double_"></a>

### ToBeGreaterThan(double)


```csharp
public void ToBeGreaterThan(double expected)
```


Requires a numeric lower bound.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_ValueExpectation_1_ToBeGreaterThanOrEqual_"></a>
<a id="E2E_ValueExpectation_1_ToBeGreaterThanOrEqual_System_Double_"></a>

### ToBeGreaterThanOrEqual(double)


```csharp
public void ToBeGreaterThanOrEqual(double expected)
```


Requires a numeric lower bound, inclusive.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_ValueExpectation_1_ToBeLessThan_"></a>
<a id="E2E_ValueExpectation_1_ToBeLessThan_System_Double_"></a>

### ToBeLessThan(double)


```csharp
public void ToBeLessThan(double expected)
```


Requires a numeric upper bound.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_ValueExpectation_1_ToBeLessThanOrEqual_"></a>
<a id="E2E_ValueExpectation_1_ToBeLessThanOrEqual_System_Double_"></a>

### ToBeLessThanOrEqual(double)


```csharp
public void ToBeLessThanOrEqual(double expected)
```


Requires a numeric upper bound, inclusive.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_ValueExpectation_1_ToBeNull_"></a>
<a id="E2E_ValueExpectation_1_ToBeNull"></a>

### ToBeNull()


```csharp
public void ToBeNull()
```


Requires `null`.

<a id="E2E_ValueExpectation_1_ToBeTruthy_"></a>
<a id="E2E_ValueExpectation_1_ToBeTruthy"></a>

### ToBeTruthy()


```csharp
public void ToBeTruthy()
```


Requires a truthy value: not `null`, [Undefined](/api/E2E/Expect/#E2E_Expect_Undefined), `false`, `0`, `NaN`, or an empty string.

<a id="E2E_ValueExpectation_1_ToBeUndefined_"></a>
<a id="E2E_ValueExpectation_1_ToBeUndefined"></a>

### ToBeUndefined()


```csharp
public void ToBeUndefined()
```


Requires [Undefined](/api/E2E/Expect/#E2E_Expect_Undefined): `null` is not `undefined`.

<a id="E2E_ValueExpectation_1_ToContain_"></a>
<a id="E2E_ValueExpectation_1_ToContain_System_Object_"></a>

### ToContain(object?)


```csharp
public void ToContain(object? expected)
```


Requires string or collection containment. A collection is searched with `ToEqual` rules.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_ValueExpectation_1_ToEqual_"></a>
<a id="E2E_ValueExpectation_1_ToEqual_System_Object_"></a>

### ToEqual(object?)


```csharp
public void ToEqual(object? expected)
```


Performs recursive structural equality. A class is not compared: a record equals a dictionary of its property names, and a map
([IDictionary](https://learn.microsoft.com/dotnet/api/system.collections.idictionary)), a set (`ISet<T>`), and an array each match by its own rule. Takes [Any(Type)](/api/E2E/Expect/#E2E_Expect_Any_System_Type_) and the
other asymmetric matchers anywhere in &lt;code class="paramref"&gt;expected&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_ValueExpectation_1_ToHaveLength_"></a>
<a id="E2E_ValueExpectation_1_ToHaveLength_System_Int32_"></a>

### ToHaveLength(int)


```csharp
public void ToHaveLength(int expected)
```


Requires a `length` equal to &lt;code class="paramref"&gt;expected&lt;/code&gt;: a string, an array, a collection, or anything with a numeric `Length` or `Count`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

<a id="E2E_ValueExpectation_1_ToHaveProperty_"></a>
<a id="E2E_ValueExpectation_1_ToHaveProperty_System_Collections_Generic_IReadOnlyList_System_Object__"></a>

### `ToHaveProperty(IReadOnlyList<object>)`


```csharp
public void ToHaveProperty(IReadOnlyList<object> path)
```


Requires the property at &lt;code class="paramref"&gt;path&lt;/code&gt;, one key per element, to exist.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

<a id="E2E_ValueExpectation_1_ToHaveProperty_System_Collections_Generic_IReadOnlyList_System_Object__System_Object_"></a>

### `ToHaveProperty(IReadOnlyList<object>, object?)`


```csharp
public void ToHaveProperty(IReadOnlyList<object> path, object? expected)
```


Requires the property at &lt;code class="paramref"&gt;path&lt;/code&gt;, one key per element, to equal &lt;code class="paramref"&gt;expected&lt;/code&gt; by `ToEqual` rules.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_ValueExpectation_1_ToHaveProperty_System_String_"></a>

### ToHaveProperty(string)


```csharp
public void ToHaveProperty(string path)
```


Requires the property at the dotted &lt;code class="paramref"&gt;path&lt;/code&gt; to exist. A key that holds a dot needs the list form.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_ValueExpectation_1_ToHaveProperty_System_String_System_Object_"></a>

### ToHaveProperty(string, object?)


```csharp
public void ToHaveProperty(string path, object? expected)
```


Requires the property at the dotted &lt;code class="paramref"&gt;path&lt;/code&gt; to equal &lt;code class="paramref"&gt;expected&lt;/code&gt; by `ToEqual` rules.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_ValueExpectation_1_ToMatch_"></a>
<a id="E2E_ValueExpectation_1_ToMatch_System_String_"></a>

### ToMatch(string)


```csharp
public void ToMatch(string expected)
```


Requires the string to contain &lt;code class="paramref"&gt;expected&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_ValueExpectation_1_ToMatch_System_Text_RegularExpressions_Regex_"></a>

### ToMatch(Regex)


```csharp
public void ToMatch(Regex expected)
```


Requires the string to match &lt;code class="paramref"&gt;expected&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |

<a id="E2E_ValueExpectation_1_ToMatchObject_"></a>
<a id="E2E_ValueExpectation_1_ToMatchObject_System_Object_"></a>

### ToMatchObject(object)


```csharp
public void ToMatchObject(object expected)
```


Requires every property of &lt;code class="paramref"&gt;expected&lt;/code&gt; to match recursively; extra properties on the value are fine, arrays must match element-wise with equal length.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_ValueExpectation_1_ToMatchSchema_"></a>
<a id="E2E_ValueExpectation_1_ToMatchSchema__1_E2E_StandardSchemaV1___0__"></a>

### `ToMatchSchema<TOutput>(StandardSchemaV1<TOutput>)`


```csharp
public TOutput? ToMatchSchema<TOutput>(StandardSchemaV1<TOutput> schema)
```


Validates the value with &lt;code class="paramref"&gt;schema&lt;/code&gt;, synchronously. Returns the schema's output (defaults applied, transforms run). A
failure lists every issue, one line each, by its path. A negated check returns `null` (the default) instead of an output.

**Type parameters**

| Name | Description |
| --- | --- |
| `TOutput` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `schema` | `StandardSchemaV1&lt;TOutput&gt;` |  |

**Returns:** `TOutput`

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `INVALID_ARGUMENT` when &lt;code class="paramref"&gt;schema&lt;/code&gt; is `null`. |
