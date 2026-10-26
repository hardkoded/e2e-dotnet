---
title: "LocatorAction.Swipe"
description: "Scrolls a scrollable node about three quarters of its height or width. The viewport swipe is CancellationToken)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.Swipe"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_Swipe"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.Swipe : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.Swipe>
```


Scrolls a scrollable node about three quarters of its height or width. The viewport swipe is `CancellationToken)`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.Swipe&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L41)

## Constructors

<a id="E2E_Engine_LocatorAction_Swipe__ctor_"></a>
<a id="E2E_Engine_LocatorAction_Swipe__ctor_E2E_Engine_ScrollDirection_System_Nullable_E2E_Engine_Momentum__"></a>

### Swipe(ScrollDirection, Momentum?)


```csharp
public Swipe(ScrollDirection Direction, Momentum? Momentum = null)
```


Scrolls a scrollable node about three quarters of its height or width. The viewport swipe is `CancellationToken)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `Momentum` | `Momentum?` |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L41)

## Properties

<a id="E2E_Engine_LocatorAction_Swipe_Direction_"></a>
<a id="E2E_Engine_LocatorAction_Swipe_Direction"></a>

### Direction


```csharp
public ScrollDirection Direction { get; init; }
```


**Value:** [ScrollDirection](/api/E2E.Engine/ScrollDirection/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L41)

<a id="E2E_Engine_LocatorAction_Swipe_Momentum_"></a>
<a id="E2E_Engine_LocatorAction_Swipe_Momentum"></a>

### Momentum


```csharp
public Momentum? Momentum { get; init; }
```


**Value:** `Momentum?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L41)
