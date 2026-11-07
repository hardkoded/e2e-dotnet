---
title: "SoftValueExpectation<T>"
description: "The value matchers of ValueExpectation<T>, but an ASSERTION_FAILED is kept on the session instead of thrown, and the test body runs on. Any other error, such..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SoftValueExpectation<T>"
  hidden: true
---

<a id="E2E_SoftValueExpectation_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SoftValueExpectation<T>
```


The value matchers of `ValueExpectation&lt;T&gt;`, but an `ASSERTION_FAILED` is kept on the session instead of thrown, and the
test body runs on. Any other error, such as `INVALID_ARGUMENT`, still throws.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs)

## Properties

<a id="E2E_SoftValueExpectation_1_Not_"></a>
<a id="E2E_SoftValueExpectation_1_Not"></a>

### Not


```csharp
public SoftValueExpectation<T> Not { get; }
```


Inverts the matcher, as `Not`. A failure is still kept, not thrown.

**Value:** [SoftValueExpectation&lt;T&gt;](./)

## Methods

<a id="E2E_SoftValueExpectation_1_ToBe_"></a>
<a id="E2E_SoftValueExpectation_1_ToBe__0_"></a>

### ToBe(T)


```csharp
public void ToBe(T expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | `T` |  |

<a id="E2E_SoftValueExpectation_1_ToBeCloseTo_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeCloseTo_System_Double_System_Int32_"></a>

### ToBeCloseTo(double, int)


```csharp
public void ToBeCloseTo(double expected, int digits = 2)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `digits` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

<a id="E2E_SoftValueExpectation_1_ToBeDefined_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeDefined"></a>

### ToBeDefined()


```csharp
public void ToBeDefined()
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

<a id="E2E_SoftValueExpectation_1_ToBeFalsy_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeFalsy"></a>

### ToBeFalsy()


```csharp
public void ToBeFalsy()
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

<a id="E2E_SoftValueExpectation_1_ToBeGreaterThan_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeGreaterThan_System_Double_"></a>

### ToBeGreaterThan(double)


```csharp
public void ToBeGreaterThan(double expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_SoftValueExpectation_1_ToBeGreaterThanOrEqual_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeGreaterThanOrEqual_System_Double_"></a>

### ToBeGreaterThanOrEqual(double)


```csharp
public void ToBeGreaterThanOrEqual(double expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_SoftValueExpectation_1_ToBeLessThan_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeLessThan_System_Double_"></a>

### ToBeLessThan(double)


```csharp
public void ToBeLessThan(double expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_SoftValueExpectation_1_ToBeLessThanOrEqual_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeLessThanOrEqual_System_Double_"></a>

### ToBeLessThanOrEqual(double)


```csharp
public void ToBeLessThanOrEqual(double expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

<a id="E2E_SoftValueExpectation_1_ToBeNull_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeNull"></a>

### ToBeNull()


```csharp
public void ToBeNull()
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

<a id="E2E_SoftValueExpectation_1_ToBeTruthy_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeTruthy"></a>

### ToBeTruthy()


```csharp
public void ToBeTruthy()
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

<a id="E2E_SoftValueExpectation_1_ToBeUndefined_"></a>
<a id="E2E_SoftValueExpectation_1_ToBeUndefined"></a>

### ToBeUndefined()


```csharp
public void ToBeUndefined()
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

<a id="E2E_SoftValueExpectation_1_ToContain_"></a>
<a id="E2E_SoftValueExpectation_1_ToContain_System_Object_"></a>

### ToContain(object?)


```csharp
public void ToContain(object? expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_SoftValueExpectation_1_ToEqual_"></a>
<a id="E2E_SoftValueExpectation_1_ToEqual_System_Object_"></a>

### ToEqual(object?)


```csharp
public void ToEqual(object? expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_SoftValueExpectation_1_ToHaveLength_"></a>
<a id="E2E_SoftValueExpectation_1_ToHaveLength_System_Int32_"></a>

### ToHaveLength(int)


```csharp
public void ToHaveLength(int expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

<a id="E2E_SoftValueExpectation_1_ToHaveProperty_"></a>
<a id="E2E_SoftValueExpectation_1_ToHaveProperty_System_Collections_Generic_IReadOnlyList_System_Object__"></a>

### `ToHaveProperty(IReadOnlyList<object>)`


```csharp
public void ToHaveProperty(IReadOnlyList<object> path)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

<a id="E2E_SoftValueExpectation_1_ToHaveProperty_System_Collections_Generic_IReadOnlyList_System_Object__System_Object_"></a>

### `ToHaveProperty(IReadOnlyList<object>, object?)`


```csharp
public void ToHaveProperty(IReadOnlyList<object> path, object? expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_SoftValueExpectation_1_ToHaveProperty_System_String_"></a>

### ToHaveProperty(string)


```csharp
public void ToHaveProperty(string path)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_SoftValueExpectation_1_ToHaveProperty_System_String_System_Object_"></a>

### ToHaveProperty(string, object?)


```csharp
public void ToHaveProperty(string path, object? expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_SoftValueExpectation_1_ToMatch_"></a>
<a id="E2E_SoftValueExpectation_1_ToMatch_System_String_"></a>

### ToMatch(string)


```csharp
public void ToMatch(string expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_SoftValueExpectation_1_ToMatch_System_Text_RegularExpressions_Regex_"></a>

### ToMatch(Regex)


```csharp
public void ToMatch(Regex expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |

<a id="E2E_SoftValueExpectation_1_ToMatchObject_"></a>
<a id="E2E_SoftValueExpectation_1_ToMatchObject_System_Object_"></a>

### ToMatchObject(object)


```csharp
public void ToMatchObject(object expected)
```


The matcher of the same name on `ValueExpectation&lt;T&gt;`, with a failure kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
