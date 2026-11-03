---
title: "PointDragResult"
description: "Upstream's PointDragResult: what one DragAtAsync did, for the executor to relay to its model."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointDragResult"
  hidden: true
---

<a id="E2E_PointDragResult"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record PointDragResult : IEquatable<PointDragResult>
```


Upstream's `PointDragResult`: what one `DragAtAsync` did, for the executor to relay to its model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PointDragResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Constructors

<a id="E2E_PointDragResult__ctor_"></a>
<a id="E2E_PointDragResult__ctor_E2E_Engine_Point_E2E_Engine_Point_E2E_ExecutorTarget_E2E_ExecutorTarget_System_String_"></a>

### PointDragResult(Point, Point, ExecutorTarget?, ExecutorTarget?, string)


```csharp
public PointDragResult(Point From, Point To, ExecutorTarget? Source, ExecutorTarget? Destination, string Summary)
```


Upstream's `PointDragResult`: what one `DragAtAsync` did, for the executor to relay to its model.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `From` | [Point](/api/E2E.Engine/Point/) | Where the start landed after clamping to the viewport, in CSS pixels. |
| `To` | [Point](/api/E2E.Engine/Point/) | Where the end landed after clamping to the viewport, in CSS pixels. |
| `Source` | [ExecutorTarget](/api/E2E/ExecutorTarget/) | The listed, enabled control holding the start, dragged by id or at the point; null when none is listed there. |
| `Destination` | [ExecutorTarget](/api/E2E/ExecutorTarget/) | The listed, enabled control holding the end; null when none is listed there. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Prose for a model: what was dragged where, or what sits under the bare points. |

## Properties

<a id="E2E_PointDragResult_Destination_"></a>
<a id="E2E_PointDragResult_Destination"></a>

### Destination


```csharp
public ExecutorTarget? Destination { get; init; }
```


The listed, enabled control holding the end; null when none is listed there.

**Value:** [ExecutorTarget](/api/E2E/ExecutorTarget/)

<a id="E2E_PointDragResult_From_"></a>
<a id="E2E_PointDragResult_From"></a>

### From


```csharp
public Point From { get; init; }
```


Where the start landed after clamping to the viewport, in CSS pixels.

**Value:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_PointDragResult_Source_"></a>
<a id="E2E_PointDragResult_Source"></a>

### Source


```csharp
public ExecutorTarget? Source { get; init; }
```


The listed, enabled control holding the start, dragged by id or at the point; null when none is listed there.

**Value:** [ExecutorTarget](/api/E2E/ExecutorTarget/)

<a id="E2E_PointDragResult_Summary_"></a>
<a id="E2E_PointDragResult_Summary"></a>

### Summary


```csharp
public string Summary { get; init; }
```


Prose for a model: what was dragged where, or what sits under the bare points.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_PointDragResult_To_"></a>
<a id="E2E_PointDragResult_To"></a>

### To


```csharp
public Point To { get; init; }
```


Where the end landed after clamping to the viewport, in CSS pixels.

**Value:** [Point](/api/E2E.Engine/Point/)
