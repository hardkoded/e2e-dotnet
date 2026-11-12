---
title: "HoverAtActionEvent"
description: "A hoverAt the step dispatched. Mobile engines have no hoverAt."
editUrl: false
prev: false
next: false
sidebar:
  label: "HoverAtActionEvent"
  hidden: true
---

<a id="E2E_HoverAtActionEvent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record HoverAtActionEvent : ActionEvent, IEquatable<ActionEvent>, IEquatable<HoverAtActionEvent>
```


A `hoverAt` the step dispatched. Mobile engines have no `hoverAt`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionEvent](/api/E2E/ActionEvent/)

**Implements:** [IEquatable&lt;ActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;HoverAtActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)

## Constructors

<a id="E2E_HoverAtActionEvent__ctor_"></a>
<a id="E2E_HoverAtActionEvent__ctor_System_Int32_System_String_System_String_E2E_ActionSource_E2E_Engine_Point_"></a>

### HoverAtActionEvent(int, string, string, ActionSource, Point)


```csharp
public HoverAtActionEvent(int Index, string Action, string Summary, ActionSource Source, Point Point)
```


A `hoverAt` the step dispatched. Mobile engines have no `hoverAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | 1-based position in the step's action budget. |
| `Action` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The committed action string. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the action did, secrets redacted. |
| `Source` | [ActionSource](/api/E2E/ActionSource/) | Who chose the action. |
| `Point` | [Point](/api/E2E.Engine/Point/) | The dispatched point, in the space of `Screen.TapAtAsync` and bounding boxes, rounded and clamped to the viewport. |

## Properties

<a id="E2E_HoverAtActionEvent_Point_"></a>
<a id="E2E_HoverAtActionEvent_Point"></a>

### Point


```csharp
public Point Point { get; init; }
```


The dispatched point, in the space of `Screen.TapAtAsync` and bounding boxes, rounded and clamped to the viewport.

**Value:** [Point](/api/E2E.Engine/Point/)
