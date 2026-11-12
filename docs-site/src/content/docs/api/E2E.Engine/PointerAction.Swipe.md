---
title: "PointerAction.Swipe"
description: "A scroll gesture started at the point, as far as Momentum says."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointerAction.Swipe"
  hidden: true
---

<a id="E2E_Engine_PointerAction_Swipe"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record PointerAction.Swipe : PointerAction, IEquatable<PointerAction>, IEquatable<PointerAction.Swipe>
```


A scroll gesture started at the point, as far as &lt;code class="paramref"&gt;Momentum&lt;/code&gt; says.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [PointerAction](/api/E2E.Engine/PointerAction/)

**Implements:** [IEquatable&lt;PointerAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;PointerAction.Swipe&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)

## Constructors

<a id="E2E_Engine_PointerAction_Swipe__ctor_"></a>
<a id="E2E_Engine_PointerAction_Swipe__ctor_E2E_Engine_ScrollDirection_System_Nullable_E2E_Engine_Momentum__"></a>

### Swipe(ScrollDirection, Momentum?)


```csharp
public Swipe(ScrollDirection Direction, Momentum? Momentum = null)
```


A scroll gesture started at the point, as far as &lt;code class="paramref"&gt;Momentum&lt;/code&gt; says.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `Momentum` | `Momentum?` |  |

## Properties

<a id="E2E_Engine_PointerAction_Swipe_Direction_"></a>
<a id="E2E_Engine_PointerAction_Swipe_Direction"></a>

### Direction


```csharp
public ScrollDirection Direction { get; init; }
```


**Value:** [ScrollDirection](/api/E2E.Engine/ScrollDirection/)

<a id="E2E_Engine_PointerAction_Swipe_Momentum_"></a>
<a id="E2E_Engine_PointerAction_Swipe_Momentum"></a>

### Momentum


```csharp
public Momentum? Momentum { get; init; }
```


**Value:** `Momentum?`
