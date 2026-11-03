---
title: "RecordedAction"
description: "RecordedAction in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedAction"
  hidden: true
---

<a id="E2E_RecordedAction"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class RecordedAction
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_RecordedAction_Direction_"></a>
<a id="E2E_RecordedAction_Direction"></a>

### Direction


```csharp
public string? Direction { get; set; }
```


`up`, `down`, `left`, or `right` for `scroll` and `scrollUntil`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_From_"></a>
<a id="E2E_RecordedAction_From"></a>

### From


```csharp
public RecordedPointEnd? From { get; set; }
```


The start of a `dragAt`, placed the way a point action's one point is.

**Value:** [RecordedPointEnd](/api/E2E/RecordedPointEnd/)

<a id="E2E_RecordedAction_Key_"></a>
<a id="E2E_RecordedAction_Key"></a>

### Key


```csharp
public string? Key { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Kind_"></a>
<a id="E2E_RecordedAction_Kind"></a>

### Kind


```csharp
public string Kind { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Modifiers_"></a>
<a id="E2E_RecordedAction_Modifiers"></a>

### Modifiers


```csharp
public List<string>? Modifiers { get; set; }
```


Keys a `tap` or `doubleTap` held for the click, replayed with it. Null holds none.

**Value:** [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

<a id="E2E_RecordedAction_Name_"></a>
<a id="E2E_RecordedAction_Name"></a>

### Name


```csharp
public string? Name { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Paths_"></a>
<a id="E2E_RecordedAction_Paths"></a>

### Paths


```csharp
public List<string>? Paths { get; set; }
```


Upstream's `paths` of an `upload`: the files attached, by project-relative path as the executor gave them. A replay input like a
typed value, expanded with the call's params and authorized against the project root again before it reaches the engine.

**Value:** [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

<a id="E2E_RecordedAction_Point_"></a>
<a id="E2E_RecordedAction_Point"></a>

### Point


```csharp
public Point? Point { get; set; }
```


The viewport point of a `tapAt` or `hoverAt`, in CSS pixels, where the model aimed (upstream's `PointAction.point`). Replayed as
given on a viewport of [Viewport](#E2E_RecordedAction_Viewport)'s size, or at [Under](#E2E_RecordedAction_Under)'s place in the live node.

**Value:** `Point?`

<a id="E2E_RecordedAction_Role_"></a>
<a id="E2E_RecordedAction_Role"></a>

### Role


```csharp
public string? Role { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Spans_"></a>
<a id="E2E_RecordedAction_Spans"></a>

### Spans


```csharp
public double? Spans { get; set; }
```


Upstream's `spans`: the share of the viewport the scrolled list covered when recorded, 0 to 1. A list that filled the screen
(`MainListShare`) scrolls as the viewport does when a replay cannot re-find it; a smaller region never does.
Null for a scroll with no list, and for a list on an engine that does not measure the viewport.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_RecordedAction_TestId_"></a>
<a id="E2E_RecordedAction_TestId"></a>

### TestId


```csharp
public string? TestId { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Text_"></a>
<a id="E2E_RecordedAction_Text"></a>

### Text


```csharp
public string? Text { get; set; }
```


The text a `scrollUntil` paged toward.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Times_"></a>
<a id="E2E_RecordedAction_Times"></a>

### Times


```csharp
public int? Times { get; set; }
```


Consecutive identical scrolls folded into one action. Null means one.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_RecordedAction_To_"></a>
<a id="E2E_RecordedAction_To"></a>

### To


```csharp
public RecordedPointEnd? To { get; set; }
```


The end of a `dragAt`.

**Value:** [RecordedPointEnd](/api/E2E/RecordedPointEnd/)

<a id="E2E_RecordedAction_Under_"></a>
<a id="E2E_RecordedAction_Under"></a>

### Under


```csharp
public RecordedPointPlace? Under { get; set; }
```


Upstream's `within` of a point action: the listed node that held the point and the point's place inside its box, when the node has
a durable descriptor. Replay re-finds the node and acts at the same place in its live box, so a layout shift moves the point with it.

**Value:** [RecordedPointPlace](/api/E2E/RecordedPointPlace/)

<a id="E2E_RecordedAction_Url_"></a>
<a id="E2E_RecordedAction_Url"></a>

### Url


```csharp
public string? Url { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Value_"></a>
<a id="E2E_RecordedAction_Value"></a>

### Value


```csharp
public string? Value { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Viewport_"></a>
<a id="E2E_RecordedAction_Viewport"></a>

### Viewport


```csharp
public TraceViewport? Viewport { get; set; }
```


The size of the viewport a point action was placed in, the precondition of its replay (upstream's `viewport` of a point action).

**Value:** [TraceViewport](/api/E2E.Engine/TraceViewport/)

<a id="E2E_RecordedAction_Within_"></a>
<a id="E2E_RecordedAction_Within"></a>

### Within


```csharp
public string? Within { get; set; }
```


Upstream's `within`: the key of the container the target sat in (a row's first cell), with secrets redacted. Relocation
requires the same key, so one row's "Delete" is never taken for another's. Null when the target sat in no named container.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
