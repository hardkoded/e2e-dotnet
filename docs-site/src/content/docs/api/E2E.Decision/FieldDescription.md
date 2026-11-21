---
title: "FieldDescription"
description: "The field a text ask describes: the element's label, role, and value (upstream's FieldInput['field'], an inline type there)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FieldDescription"
  hidden: true
---

<a id="E2E_Decision_FieldDescription"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record FieldDescription : IEquatable<FieldDescription>
```


The field a text ask describes: the element's label, role, and value (upstream's `FieldInput['field']`, an inline type there).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;FieldDescription&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Constructors

<a id="E2E_Decision_FieldDescription__ctor_"></a>
<a id="E2E_Decision_FieldDescription__ctor_System_String_System_String_System_String_"></a>

### FieldDescription(string, string, string?)


```csharp
public FieldDescription(string Label, string Role, string? Value = null)
```


The field a text ask describes: the element's label, role, and value (upstream's `FieldInput['field']`, an inline type there).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_FieldDescription_Label_"></a>
<a id="E2E_Decision_FieldDescription_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_FieldDescription_Role_"></a>
<a id="E2E_Decision_FieldDescription_Role"></a>

### Role


```csharp
public string Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_FieldDescription_Value_"></a>
<a id="E2E_Decision_FieldDescription_Value"></a>

### Value


```csharp
public string? Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
