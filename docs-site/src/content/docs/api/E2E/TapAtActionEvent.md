---
title: "TapAtActionEvent"
description: "A tapAt the step dispatched. Upstream's ActionEvent narrowed to tapAt."
editUrl: false
prev: false
next: false
sidebar:
  label: "TapAtActionEvent"
  hidden: true
---

<a id="E2E_TapAtActionEvent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record TapAtActionEvent : ActionEvent, IEquatable<ActionEvent>, IEquatable<TapAtActionEvent>
```


A `tapAt` the step dispatched. Upstream's `ActionEvent` narrowed to `tapAt`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionEvent](/api/E2E/ActionEvent/)

**Implements:** [IEquatable&lt;ActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;TapAtActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)

## Constructors

<a id="E2E_TapAtActionEvent__ctor_"></a>
<a id="E2E_TapAtActionEvent__ctor_System_Int32_System_String_System_String_E2E_ActionSource_E2E_Engine_Point_"></a>

### TapAtActionEvent(int, string, string, ActionSource, Point)


```csharp
public TapAtActionEvent(int Index, string Action, string Summary, ActionSource Source, Point Point)
```


A `tapAt` the step dispatched. Upstream's `ActionEvent` narrowed to `tapAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | 1-based position in the step's action budget. |
| `Action` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The committed action string. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the action did, secrets redacted. |
| `Source` | [ActionSource](/api/E2E/ActionSource/) | Who chose the action. |
| `Point` | [Point](/api/E2E.Engine/Point/) | The dispatched point, in the space of `Screen.TapAtAsync` and bounding boxes, rounded and clamped to the viewport. |

## Properties

<a id="E2E_TapAtActionEvent_Point_"></a>
<a id="E2E_TapAtActionEvent_Point"></a>

### Point


```csharp
public Point Point { get; init; }
```


The dispatched point, in the space of `Screen.TapAtAsync` and bounding boxes, rounded and clamped to the viewport.

**Value:** [Point](/api/E2E.Engine/Point/)
