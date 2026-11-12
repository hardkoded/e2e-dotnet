---
title: "DragAtActionEvent"
description: "A dragAt the step dispatched. Mobile engines have no dragAt."
editUrl: false
prev: false
next: false
sidebar:
  label: "DragAtActionEvent"
  hidden: true
---

<a id="E2E_DragAtActionEvent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record DragAtActionEvent : ActionEvent, IEquatable<ActionEvent>, IEquatable<DragAtActionEvent>
```


A `dragAt` the step dispatched. Mobile engines have no `dragAt`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionEvent](/api/E2E/ActionEvent/)

**Implements:** [IEquatable&lt;ActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DragAtActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)

## Constructors

<a id="E2E_DragAtActionEvent__ctor_"></a>
<a id="E2E_DragAtActionEvent__ctor_System_Int32_System_String_System_String_E2E_ActionSource_E2E_Engine_Point_E2E_Engine_Point_"></a>

### DragAtActionEvent(int, string, string, ActionSource, Point, Point)


```csharp
public DragAtActionEvent(int Index, string Action, string Summary, ActionSource Source, Point From, Point To)
```


A `dragAt` the step dispatched. Mobile engines have no `dragAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | 1-based position in the step's action budget. |
| `Action` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The committed action string. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the action did, secrets redacted. |
| `Source` | [ActionSource](/api/E2E/ActionSource/) | Who chose the action. |
| `From` | [Point](/api/E2E.Engine/Point/) | The dispatched start, rounded and clamped to the viewport. |
| `To` | [Point](/api/E2E.Engine/Point/) | The dispatched end, rounded and clamped to the viewport. |

## Properties

<a id="E2E_DragAtActionEvent_From_"></a>
<a id="E2E_DragAtActionEvent_From"></a>

### From


```csharp
public Point From { get; init; }
```


The dispatched start, rounded and clamped to the viewport.

**Value:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_DragAtActionEvent_To_"></a>
<a id="E2E_DragAtActionEvent_To"></a>

### To


```csharp
public Point To { get; init; }
```


The dispatched end, rounded and clamped to the viewport.

**Value:** [Point](/api/E2E.Engine/Point/)
