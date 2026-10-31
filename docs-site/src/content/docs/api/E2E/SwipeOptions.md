---
title: "SwipeOptions"
description: "Options for SwipeAsync."
editUrl: false
prev: false
next: false
sidebar:
  label: "SwipeOptions"
  hidden: true
---

<a id="E2E_SwipeOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SwipeOptions : ActionOptions
```


Options for `SwipeAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionOptions](/api/E2E/ActionOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1241)

## Properties

<a id="E2E_SwipeOptions_Direction_"></a>
<a id="E2E_SwipeOptions_Direction"></a>

### Direction


```csharp
public required ScrollDirection Direction { get; init; }
```


Which way the content moves into view, as for [ScrollDirection](/api/E2E.Engine/ScrollDirection/).

**Value:** [ScrollDirection](/api/E2E.Engine/ScrollDirection/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1244)

<a id="E2E_SwipeOptions_Momentum_"></a>
<a id="E2E_SwipeOptions_Momentum"></a>

### Momentum


```csharp
public Momentum? Momentum { get; init; }
```


The stride of the gesture. Without one the engine strides as it does for a scroll.

**Value:** `Momentum?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1247)
