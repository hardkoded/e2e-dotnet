---
title: "Grid"
description: "A grid over an image: at most ten columns and rows, one score level per column and row (upstream's Grid). Sizes are in the CSS pixels of the image's coordina..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Grid"
  hidden: true
---

<a id="E2E_Decision_Grid"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Grid : IEquatable<Grid>
```


A grid over an image: at most ten columns and rows, one score level per column and row (upstream's `Grid`). Sizes are in the CSS
pixels of the image's coordinates.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Grid&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Overlay.cs)

## Constructors

<a id="E2E_Decision_Grid__ctor_"></a>
<a id="E2E_Decision_Grid__ctor_System_Int32_System_Int32_System_Double_System_Double_"></a>

### Grid(int, int, double, double)


```csharp
public Grid(int Columns, int Rows, double CellWidth, double CellHeight)
```


A grid over an image: at most ten columns and rows, one score level per column and row (upstream's `Grid`). Sizes are in the CSS
pixels of the image's coordinates.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Columns` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Rows` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `CellWidth` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `CellHeight` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Decision_Grid_CellHeight_"></a>
<a id="E2E_Decision_Grid_CellHeight"></a>

### CellHeight


```csharp
public double CellHeight { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Grid_CellWidth_"></a>
<a id="E2E_Decision_Grid_CellWidth"></a>

### CellWidth


```csharp
public double CellWidth { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Grid_Columns_"></a>
<a id="E2E_Decision_Grid_Columns"></a>

### Columns


```csharp
public int Columns { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Grid_Rows_"></a>
<a id="E2E_Decision_Grid_Rows"></a>

### Rows


```csharp
public int Rows { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
