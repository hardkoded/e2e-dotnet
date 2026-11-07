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

<a id="E2E_RecordedAction_Derived_"></a>
<a id="E2E_RecordedAction_Derived"></a>

### Derived


```csharp
public DerivedReason? Derived { get; set; }
```


Upstream's `derived` of a `tool` gap: the rule that made a typed value this run's data ([DerivedReason](/api/E2E/DerivedReason/)), set only
on the gap a fill recorded in place of its value. A replay stops at the gap whatever its rule, which only names the hand-off.

**Value:** `DerivedReason?`

<a id="E2E_RecordedAction_Destination_"></a>
<a id="E2E_RecordedAction_Destination"></a>

### Destination


```csharp
public RecordedTarget? Destination { get; set; }
```


Upstream's `destination` of a `drag`: the node the source was dropped on, described like the source with the container it
sat in. A replay re-finds both ends on one screen before it drags. Null on every other action.

**Value:** [RecordedTarget](/api/E2E/RecordedTarget/)

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

<a id="E2E_RecordedAction_InputPurpose_"></a>
<a id="E2E_RecordedAction_InputPurpose"></a>

### InputPurpose


```csharp
public string? InputPurpose { get; set; }
```


Upstream's `inputPurpose` of the target descriptor, such as `password`. Null for `none` and for a target that is no field.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

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


Keys a `tap`, `doubleTap`, or `secondaryTap` held for the click, replayed with it. Null holds none.

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

<a id="E2E_RecordedAction_Placeholder_"></a>
<a id="E2E_RecordedAction_Placeholder"></a>

### Placeholder


```csharp
public string? Placeholder { get; set; }
```


Upstream's `placeholder` of the target descriptor: the placeholder of the field the action named, with secrets redacted. A field
named only by its placeholder relocates by it. Null when the target has none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Point_"></a>
<a id="E2E_RecordedAction_Point"></a>

### Point


```csharp
public Point? Point { get; set; }
```


The viewport point of a `tapAt` or `hoverAt`, in CSS pixels, where the model aimed (upstream's `PointAction.point`). Replayed as
given on a viewport of [Viewport](#E2E_RecordedAction_Viewport)'s size, or at [Under](#E2E_RecordedAction_Under)'s place in the live node.

**Value:** `Point?`

<a id="E2E_RecordedAction_Position_"></a>
<a id="E2E_RecordedAction_Position"></a>

### Position


```csharp
public TracePosition? Position { get; set; }
```


Upstream's `position` of the target descriptor: where the target stood among the controls its own description matched on the screen it
was acted on (`DescribePosition`). Null when the description named it alone.

**Value:** [TracePosition](/api/E2E/TracePosition/)

<a id="E2E_RecordedAction_Replace_"></a>
<a id="E2E_RecordedAction_Replace"></a>

### Replace


```csharp
public bool? Replace { get; set; }
```


Upstream's `replace` of a `typeText`: whether the field's value was cleared before the text went in. Present on a
`typeText` and on no other action.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_RecordedAction_Role_"></a>
<a id="E2E_RecordedAction_Role"></a>

### Role


```csharp
public string? Role { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Size_"></a>
<a id="E2E_RecordedAction_Size"></a>

### Size


```csharp
public TraceViewport? Size { get; set; }
```


The viewport a `resize` set, in CSS pixels, replayed as given (upstream's `size` of a `ResizeAction`). Null on every other action.

**Value:** [TraceViewport](/api/E2E.Engine/TraceViewport/)

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

<a id="E2E_RecordedAction_Summary_"></a>
<a id="E2E_RecordedAction_Summary"></a>

### Summary


```csharp
public string? Summary { get; set; }
```


Upstream's `summary` of a recorded action (`AgentActions.DescribeAction`): redacted, bounded prose, `tap button "Approve"`,
the only view a mid-step hand-off notice shows. Secret values never appear, a secret's name stands in. Null on an action recorded before
the summary was stored: upstream refuses such an entry, the port keeps reading it (.NET-only leniency, so recorded caches stay readable).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

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


The text a `scrollUntil` paged toward and the name of a project tool a `tool` gap ran; on every other action, upstream's
`text` of the target descriptor: the text of the node when it differs from its name, with secrets redacted.

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
