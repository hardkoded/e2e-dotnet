---
title: "PointerAction.SwipeTo"
description: "A drag to another point. With a duration the finger takes that long to get there."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointerAction.SwipeTo"
  hidden: true
---

<a id="E2E_Engine_PointerAction_SwipeTo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record PointerAction.SwipeTo : PointerAction, IEquatable<PointerAction>, IEquatable<PointerAction.SwipeTo>
```


A drag to another point. With a duration the finger takes that long to get there.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [PointerAction](/api/E2E.Engine/PointerAction/)

**Implements:** [IEquatable&lt;PointerAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;PointerAction.SwipeTo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)

## Constructors

<a id="E2E_Engine_PointerAction_SwipeTo__ctor_"></a>
<a id="E2E_Engine_PointerAction_SwipeTo__ctor_E2E_Engine_Point_System_Nullable_System_TimeSpan__"></a>

### SwipeTo(Point, TimeSpan?)


```csharp
public SwipeTo(Point Target, TimeSpan? Duration = null)
```


A drag to another point. With a duration the finger takes that long to get there.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [Point](/api/E2E.Engine/Point/) |  |
| `Duration` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

## Properties

<a id="E2E_Engine_PointerAction_SwipeTo_Duration_"></a>
<a id="E2E_Engine_PointerAction_SwipeTo_Duration"></a>

### Duration


```csharp
public TimeSpan? Duration { get; init; }
```


**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Engine_PointerAction_SwipeTo_Target_"></a>
<a id="E2E_Engine_PointerAction_SwipeTo_Target"></a>

### Target


```csharp
public Point Target { get; init; }
```


**Value:** [Point](/api/E2E.Engine/Point/)
