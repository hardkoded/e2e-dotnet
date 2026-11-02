---
title: "SelectOption"
description: "One option of a select-like control, named by exactly one of its Label, its Value, or its Index. Upstream's SelectOption object forms { label }, { value }, a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SelectOption"
  hidden: true
---

<a id="E2E_SelectOption"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record SelectOption : IEquatable<SelectOption>
```


One option of a select-like control, named by exactly one of its [Label](#E2E_SelectOption_Label), its [Value](#E2E_SelectOption_Value), or its
[Index](#E2E_SelectOption_Index). Upstream's `SelectOption` object forms `{ label }`, `{ value }`, and `{ index }`; the plain
string form is `SelectOptionAsync`. A select takes one option per call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SelectOption&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/SelectOption.cs)

## Properties

<a id="E2E_SelectOption_Index_"></a>
<a id="E2E_SelectOption_Index"></a>

### Index


```csharp
public int? Index { get; init; }
```


The option's position in its list, from 0.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_SelectOption_Label_"></a>
<a id="E2E_SelectOption_Label"></a>

### Label


```csharp
public string? Label { get; init; }
```


The option's visible label.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_SelectOption_Value_"></a>
<a id="E2E_SelectOption_Value"></a>

### Value


```csharp
public string? Value { get; init; }
```


The option's `value` attribute.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
