---
title: "Element"
description: "One row of the element table the decision model reads (upstream's Element)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Element"
  hidden: true
---

<a id="E2E_Decision_Element"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Element : IEquatable<Element>
```


One row of the element table the decision model reads (upstream's `Element`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Element&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Elements.cs)

## Constructors

<a id="E2E_Decision_Element__ctor_"></a>
<a id="E2E_Decision_Element__ctor_System_String_System_String_System_String_System_String_System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Collections_Generic_IReadOnlyList_E2E_Decision_Operation__"></a>

### `Element(string, string, string, string?, bool?, bool?, bool?, IReadOnlyList<Operation>)`


```csharp
public Element(string Index, string Role, string Label, string? Value, bool? Checked, bool? Expanded, bool? Selected, IReadOnlyList<Operation> Operations)
```


One row of the element table the decision model reads (upstream's `Element`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Checked` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Expanded` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Selected` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Operations` | [IReadOnlyList&lt;Operation&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Decision_Element_Checked_"></a>
<a id="E2E_Decision_Element_Checked"></a>

### Checked


```csharp
public bool? Checked { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Element_Expanded_"></a>
<a id="E2E_Decision_Element_Expanded"></a>

### Expanded


```csharp
public bool? Expanded { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Element_Index_"></a>
<a id="E2E_Decision_Element_Index"></a>

### Index


```csharp
public string Index { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Element_Label_"></a>
<a id="E2E_Decision_Element_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Element_Operations_"></a>
<a id="E2E_Decision_Element_Operations"></a>

### Operations


```csharp
public IReadOnlyList<Operation> Operations { get; init; }
```


**Value:** [IReadOnlyList&lt;Operation&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Element_Role_"></a>
<a id="E2E_Decision_Element_Role"></a>

### Role


```csharp
public string Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Element_Selected_"></a>
<a id="E2E_Decision_Element_Selected"></a>

### Selected


```csharp
public bool? Selected { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Element_Value_"></a>
<a id="E2E_Decision_Element_Value"></a>

### Value


```csharp
public string? Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
