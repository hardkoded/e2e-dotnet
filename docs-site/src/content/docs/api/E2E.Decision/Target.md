---
title: "Target"
description: "One target of an operation: the node it acts on, as data. The model only ever names the key (upstream's Target)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Target"
  hidden: true
---

<a id="E2E_Decision_Target"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Target : IEquatable<Target>
```


One target of an operation: the node it acts on, as data. The model only ever names the key (upstream's `Target`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Target&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Elements.cs)

## Constructors

<a id="E2E_Decision_Target__ctor_"></a>
<a id="E2E_Decision_Target__ctor_System_String_System_String_System_String_System_Nullable_System_Boolean__"></a>

### Target(string, string, string?, bool?)


```csharp
public Target(string Id, string Description, string? OptionLabel = null, bool? Checked = null)
```


One target of an operation: the node it acts on, as data. The model only ever names the key (upstream's `Target`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The node's id in the observation. |
| `Description` | [string](https://learn.microsoft.com/dotnet/api/system.string) | How the action reads in history, e.g. `tap Save [n4]`. |
| `OptionLabel` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Native-select option label, set only for `select` options; names the choice criterion. |
| `Checked` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) | The checked state a `check` flips. |

## Properties

<a id="E2E_Decision_Target_Checked_"></a>
<a id="E2E_Decision_Target_Checked"></a>

### Checked


```csharp
public bool? Checked { get; init; }
```


The checked state a `check` flips.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Target_Description_"></a>
<a id="E2E_Decision_Target_Description"></a>

### Description


```csharp
public string Description { get; init; }
```


How the action reads in history, e.g. `tap Save [n4]`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Target_Id_"></a>
<a id="E2E_Decision_Target_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


The node's id in the observation.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Target_OptionLabel_"></a>
<a id="E2E_Decision_Target_OptionLabel"></a>

### OptionLabel


```csharp
public string? OptionLabel { get; init; }
```


Native-select option label, set only for `select` options; names the choice criterion.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
