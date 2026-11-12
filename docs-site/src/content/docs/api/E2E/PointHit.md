---
title: "PointHit"
description: "Upstream's PointHit: what one HitTestAsync found, for the executor to act on or relay."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointHit"
  hidden: true
---

<a id="E2E_PointHit"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record PointHit : IEquatable<PointHit>
```


Upstream's `PointHit`: what one `HitTestAsync` found, for the executor to act on or relay.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PointHit&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Constructors

<a id="E2E_PointHit__ctor_"></a>
<a id="E2E_PointHit__ctor_E2E_Engine_Point_E2E_ExecutorTarget_E2E_ExecutorTarget_System_String_"></a>

### PointHit(Point, ExecutorTarget?, ExecutorTarget?, string)


```csharp
public PointHit(Point Point, ExecutorTarget? Control, ExecutorTarget? Under, string Summary)
```


Upstream's `PointHit`: what one `HitTestAsync` found, for the executor to act on or relay.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Point` | [Point](/api/E2E.Engine/Point/) | Where the point landed after clamping to the viewport, in CSS pixels. |
| `Control` | [ExecutorTarget](/api/E2E/ExecutorTarget/) | The innermost enabled control containing the point, if any is listed. |
| `Under` | [ExecutorTarget](/api/E2E/ExecutorTarget/) | The innermost listed node of any role containing the point, if any. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Prose for a model: the control the point is on, or what sits under it. |

## Properties

<a id="E2E_PointHit_Control_"></a>
<a id="E2E_PointHit_Control"></a>

### Control


```csharp
public ExecutorTarget? Control { get; init; }
```


The innermost enabled control containing the point, if any is listed.

**Value:** [ExecutorTarget](/api/E2E/ExecutorTarget/)

<a id="E2E_PointHit_Point_"></a>
<a id="E2E_PointHit_Point"></a>

### Point


```csharp
public Point Point { get; init; }
```


Where the point landed after clamping to the viewport, in CSS pixels.

**Value:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_PointHit_Summary_"></a>
<a id="E2E_PointHit_Summary"></a>

### Summary


```csharp
public string Summary { get; init; }
```


Prose for a model: the control the point is on, or what sits under it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_PointHit_Under_"></a>
<a id="E2E_PointHit_Under"></a>

### Under


```csharp
public ExecutorTarget? Under { get; init; }
```


The innermost listed node of any role containing the point, if any.

**Value:** [ExecutorTarget](/api/E2E/ExecutorTarget/)
