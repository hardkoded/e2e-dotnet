---
title: "Point"
description: "A point of the viewport, in the space Rect is in and PerformAtAsync dispatches in. Upstream's ViewportPoint."
editUrl: false
prev: false
next: false
sidebar:
  label: "Point"
  hidden: true
---

<a id="E2E_Engine_Point"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public readonly record struct Point : IEquatable<Point>
```


A point of the viewport, in the space [Rect](/api/E2E.Engine/SemanticNode/#E2E_Engine_SemanticNode_Rect) is in and `PerformAtAsync` dispatches in.
Upstream's `ViewportPoint`.

**Implements:** [IEquatable&lt;Point&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)

## Constructors

<a id="E2E_Engine_Point__ctor_"></a>
<a id="E2E_Engine_Point__ctor_System_Double_System_Double_"></a>

### Point(double, double)


```csharp
public Point(double X, double Y)
```


A point of the viewport, in the space [Rect](/api/E2E.Engine/SemanticNode/#E2E_Engine_SemanticNode_Rect) is in and `PerformAtAsync` dispatches in.
Upstream's `ViewportPoint`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `X` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Y` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Engine_Point_X_"></a>
<a id="E2E_Engine_Point_X"></a>

### X


```csharp
public double X { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Engine_Point_Y_"></a>
<a id="E2E_Engine_Point_Y"></a>

### Y


```csharp
public double Y { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)
