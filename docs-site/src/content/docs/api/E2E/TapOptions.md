---
title: "TapOptions"
description: "Options for TapAsync and ClickAsync. Upstream's TapOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "TapOptions"
  hidden: true
---

<a id="E2E_TapOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class TapOptions : ClickOptions
```


Options for `TapAsync` and `ClickAsync`. Upstream's `TapOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionOptions](/api/E2E/ActionOptions/) → [ClickOptions](/api/E2E/ClickOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs)

## Properties

<a id="E2E_TapOptions_Count_"></a>
<a id="E2E_TapOptions_Count"></a>

### Count


```csharp
public long? Count { get; init; }
```


How many taps in a row, a positive integer, default 1. `2` is the same as `DoubleTapAsync()`; 3 or more needs an engine that declares the
`multiTap` action, `UNSUPPORTED_CAPABILITY` otherwise. A count above the engine's own limit (200 on the mobile engine) is also
`UNSUPPORTED_CAPABILITY`. A count above 1 is not combined with [Position](#E2E_TapOptions_Position).

**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

<a id="E2E_TapOptions_Position_"></a>
<a id="E2E_TapOptions_Position"></a>

### Position


```csharp
public Point? Position { get; init; }
```


Where to tap, relative to the node's top-left corner, inside its border on a document platform. Without it the platform picks a point of the node, usually its center, behind
its own actionability checks; with it the pointer is dispatched at that point once the node is in view and has a box, which needs
the engine's `tap` pointer action. Not held with [Modifiers](/api/E2E/ClickOptions/#E2E_ClickOptions_Modifiers) yet.

**Value:** `Point?`
