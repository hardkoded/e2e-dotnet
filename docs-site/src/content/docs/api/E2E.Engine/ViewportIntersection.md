---
title: "ViewportIntersection"
description: "How much of a node's box is inside the viewport, after clipping by the viewport and by every ancestor that clips its content (a scroll container, overflow: h..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ViewportIntersection"
  hidden: true
---

<a id="E2E_Engine_ViewportIntersection"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ViewportIntersection : IEquatable<ViewportIntersection>
```


How much of a node's box is inside the viewport, after clipping by the viewport and by every ancestor that clips its content (a scroll
container, `overflow: hidden`, a frame), as [ViewportIntersection](/api/E2E.Engine/EngineSpec/#E2E_Engine_EngineSpec_ViewportIntersection) measures it. It says nothing about another
node covering the box. Upstream's `ViewportIntersection` (`engine/contract.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ViewportIntersection&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_ViewportIntersection__ctor_"></a>
<a id="E2E_Engine_ViewportIntersection__ctor_System_Boolean_System_Nullable_System_Double__"></a>

### ViewportIntersection(bool, double?)


```csharp
public ViewportIntersection(bool Intersects, double? Ratio = null)
```


How much of a node's box is inside the viewport, after clipping by the viewport and by every ancestor that clips its content (a scroll
container, `overflow: hidden`, a frame), as [ViewportIntersection](/api/E2E.Engine/EngineSpec/#E2E_Engine_EngineSpec_ViewportIntersection) measures it. It says nothing about another
node covering the box. Upstream's `ViewportIntersection` (`engine/contract.ts`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Intersects` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) | Whether a positive area of the box is inside; false for a node with no box or an empty one. |
| `Ratio` | [double?](https://learn.microsoft.com/dotnet/api/system.double) | The inside area over the whole box's area, from 0 to 1. Null where the platform reports only the visible part of a box, so the whole is unknown. |

## Properties

<a id="E2E_Engine_ViewportIntersection_Intersects_"></a>
<a id="E2E_Engine_ViewportIntersection_Intersects"></a>

### Intersects


```csharp
public bool Intersects { get; init; }
```


Whether a positive area of the box is inside; false for a node with no box or an empty one.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_ViewportIntersection_Ratio_"></a>
<a id="E2E_Engine_ViewportIntersection_Ratio"></a>

### Ratio


```csharp
public double? Ratio { get; init; }
```


The inside area over the whole box's area, from 0 to 1. Null where the platform reports only the visible part of a box, so the whole is
unknown.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)
