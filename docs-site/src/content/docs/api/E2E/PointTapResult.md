---
title: "PointTapResult"
description: "Upstream's PointTapResult: what one point action (TapAtAsync, HoverAtAsync) did, for the executor to relay to its model."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointTapResult"
  hidden: true
---

<a id="E2E_PointTapResult"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record PointTapResult : IEquatable<PointTapResult>
```


Upstream's `PointTapResult`: what one point action (`TapAtAsync`, `HoverAtAsync`) did, for the executor to relay to its model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PointTapResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Constructors

<a id="E2E_PointTapResult__ctor_"></a>
<a id="E2E_PointTapResult__ctor_E2E_Engine_Point_E2E_ExecutorTarget_System_String_"></a>

### PointTapResult(Point, ExecutorTarget?, string)


```csharp
public PointTapResult(Point Point, ExecutorTarget? Target, string Summary)
```


Upstream's `PointTapResult`: what one point action (`TapAtAsync`, `HoverAtAsync`) did, for the executor to relay to its model.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Point` | [Point](/api/E2E.Engine/Point/) | Where the point landed after clamping to the viewport, in CSS pixels. |
| `Target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) | The listed, enabled control containing the point, acted on by id or at the point; null when none is listed there. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Prose for a model: what was acted on, or what sits under a bare point. |

## Properties

<a id="E2E_PointTapResult_Point_"></a>
<a id="E2E_PointTapResult_Point"></a>

### Point


```csharp
public Point Point { get; init; }
```


Where the point landed after clamping to the viewport, in CSS pixels.

**Value:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_PointTapResult_Summary_"></a>
<a id="E2E_PointTapResult_Summary"></a>

### Summary


```csharp
public string Summary { get; init; }
```


Prose for a model: what was acted on, or what sits under a bare point.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_PointTapResult_Target_"></a>
<a id="E2E_PointTapResult_Target"></a>

### Target


```csharp
public ExecutorTarget? Target { get; init; }
```


The listed, enabled control containing the point, acted on by id or at the point; null when none is listed there.

**Value:** [ExecutorTarget](/api/E2E/ExecutorTarget/)
