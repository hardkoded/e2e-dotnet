---
title: "Overlay"
description: "Upstream's overlay.ts. A viewport point is an Point, in CSS pixels."
editUrl: false
prev: false
next: false
sidebar:
  label: "Overlay"
  hidden: true
---

<a id="E2E_Decision_Overlay"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Overlay
```


Upstream's `overlay.ts`. A viewport point is an [Point](/api/E2E.Engine/Point/), in CSS pixels.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Overlay.cs)

## Fields

<a id="E2E_Decision_Overlay_CellTarget"></a>

### CellTarget


```csharp
public const double CellTarget = 80
```


Target cell size of a first look in CSS pixels (upstream's `CELL_TARGET`). A 1280 by 720 viewport gets 10 columns of 128px and
9 rows of 80px.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

## Methods

<a id="E2E_Decision_Overlay_GridFor_"></a>
<a id="E2E_Decision_Overlay_GridFor_System_Double_System_Double_System_Double_"></a>

### GridFor(double, double, double)


```csharp
public static Grid GridFor(double width, double height, double cellTarget)
```


Upstream's `gridFor`: the grid for an image, as close to &lt;code class="paramref"&gt;cellTarget&lt;/code&gt; CSS pixels per cell as the level cap allows.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `width` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `height` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `cellTarget` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [Grid](/api/E2E.Decision/Grid/)

<a id="E2E_Decision_Overlay_PointOf_"></a>
<a id="E2E_Decision_Overlay_PointOf_E2E_Decision_Grid_System_Double_System_Double_"></a>

### PointOf(Grid, double, double)


```csharp
public static Point PointOf(Grid grid, double column, double row)
```


Upstream's `pointOf`: the point a column and row score name on a grid: the center of the scored position, inside the image.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `grid` | [Grid](/api/E2E.Decision/Grid/) |  |
| `column` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `row` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_Decision_Overlay_RegionHash_"></a>
<a id="E2E_Decision_Overlay_RegionHash_E2E_Decision_Screenshot_E2E_Engine_Point_System_Double_"></a>

### RegionHash(Screenshot?, Point, double)


```csharp
public static string? RegionHash(Screenshot? screenshot, Point center, double box)
```


Upstream's `regionHash`: a hash of the &lt;code class="paramref"&gt;box&lt;/code&gt; by &lt;code class="paramref"&gt;box&lt;/code&gt; CSS-pixel region around &lt;code class="paramref"&gt;center&lt;/code&gt;: what a tap
there changed, read without the rest of the page. Null without pixels or when they cannot be decoded.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `center` | [Point](/api/E2E.Engine/Point/) |  |
| `box` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Overlay_WithGrid_"></a>
<a id="E2E_Decision_Overlay_WithGrid_E2E_Decision_Screenshot_E2E_Decision_Grid_"></a>

### WithGrid(Screenshot, Grid)


```csharp
public static Screenshot WithGrid(Screenshot screenshot, Grid grid)
```


Upstream's `withGrid`: the screenshot with the grid's cell borders drawn on it. Returns the input untouched when it cannot be decoded.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `grid` | [Grid](/api/E2E.Decision/Grid/) |  |

**Returns:** [Screenshot](/api/E2E.Decision/Screenshot/)

<a id="E2E_Decision_Overlay_ZoomAround_"></a>
<a id="E2E_Decision_Overlay_ZoomAround_E2E_Decision_Screenshot_E2E_Engine_Point_System_Double_System_Double_"></a>

### ZoomAround(Screenshot, Point, double, double)


```csharp
public static Zoom? ZoomAround(Screenshot screenshot, Point center, double box, double factor)
```


Upstream's `zoomAround`: the &lt;code class="paramref"&gt;box&lt;/code&gt; by &lt;code class="paramref"&gt;box&lt;/code&gt; CSS-pixel region around &lt;code class="paramref"&gt;center&lt;/code&gt;, kept inside the
screenshot and enlarged &lt;code class="paramref"&gt;factor&lt;/code&gt; times (nearest neighbor), for a finer closer look at a drawn control. Null when
the pixels cannot be decoded or the region comes out under one pixel, which no PNG can hold.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `center` | [Point](/api/E2E.Engine/Point/) |  |
| `box` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `factor` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [Zoom](/api/E2E.Decision/Zoom/)
