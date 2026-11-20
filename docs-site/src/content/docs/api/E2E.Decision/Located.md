---
title: "Located"
description: "Where a drawn control is, or why the looks could not say (upstream's Located): a point, or an uncertainty."
editUrl: false
prev: false
next: false
sidebar:
  label: "Located"
  hidden: true
---

<a id="E2E_Decision_Located"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Located : IEquatable<Located>
```


Where a drawn control is, or why the looks could not say (upstream's `Located`): a point, or an uncertainty.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Located&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Locator.cs)

## Properties

<a id="E2E_Decision_Located_Point_"></a>
<a id="E2E_Decision_Located_Point"></a>

### Point


```csharp
public Point? Point { get; }
```


**Value:** `Point?`

<a id="E2E_Decision_Located_Uncertain_"></a>
<a id="E2E_Decision_Located_Uncertain"></a>

### Uncertain


```csharp
public string? Uncertain { get; }
```


Why the looks could not say where the control is.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Located_At_"></a>
<a id="E2E_Decision_Located_At_E2E_Engine_Point_"></a>

### At(Point)


```csharp
public static Located At(Point point)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |

**Returns:** [Located](./)

<a id="E2E_Decision_Located_Unsure_"></a>
<a id="E2E_Decision_Located_Unsure_System_String_"></a>

### Unsure(string)


```csharp
public static Located Unsure(string why)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `why` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Located](./)
