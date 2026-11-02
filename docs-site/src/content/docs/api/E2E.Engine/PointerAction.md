---
title: "PointerAction"
description: "One action PerformAtAsync carries out at a viewport point, with no node behind it: the pointer half of the action vocabulary, for a surface that has pixels a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointerAction"
  hidden: true
---

<a id="E2E_Engine_PointerAction"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public abstract record PointerAction : IEquatable<PointerAction>
```


One action `PerformAtAsync` carries out at a viewport point, with no node behind it: the pointer half of
the action vocabulary, for a surface that has pixels and coordinates where the tree lists nothing (a canvas, a map, a desktop shell, a
game), and for a test that names a point outright (`Screen.TapAtAsync`, `Screen.SwipeAsync` from one point to another, a
tap's `Position`). The kinds are the pointer subset of [LocatorAction](/api/E2E.Engine/LocatorAction/) with the same meanings; [PointerAction.DragTo](/api/E2E.Engine/PointerAction.DragTo/)
ends at a second point, and [PointerAction.Swipe](/api/E2E.Engine/PointerAction.Swipe/) is a scroll gesture started at the point. [PointerAction.SwipeTo](/api/E2E.Engine/PointerAction.SwipeTo/) is the one kind with
no node form: the finger's path from the point to its target, a touch swipe on a device and a pointer drag on a document platform,
which is distinct from [PointerAction.DragTo](/api/E2E.Engine/PointerAction.DragTo/) where a platform holds before it drags. Upstream's `PointerAction`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PointerAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [PointerAction.DoubleTap](/api/E2E.Engine/PointerAction.DoubleTap/), [PointerAction.DragTo](/api/E2E.Engine/PointerAction.DragTo/), [PointerAction.Hover](/api/E2E.Engine/PointerAction.Hover/), [PointerAction.LongPress](/api/E2E.Engine/PointerAction.LongPress/), [PointerAction.SecondaryTap](/api/E2E.Engine/PointerAction.SecondaryTap/), [PointerAction.Swipe](/api/E2E.Engine/PointerAction.Swipe/), [PointerAction.SwipeTo](/api/E2E.Engine/PointerAction.SwipeTo/), [PointerAction.Tap](/api/E2E.Engine/PointerAction.Tap/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)
