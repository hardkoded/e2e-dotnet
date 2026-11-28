---
title: "LocatorAction.ScrollTo"
description: "Scrolls a container to one of its edges, as far as it goes. On a node: the node when it scrolls along the edge's axis, else its nearest ancestor that does; a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.ScrollTo"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_ScrollTo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.ScrollTo : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.ScrollTo>
```


Scrolls a container to one of its edges, as far as it goes. On a node: the node when it scrolls along the edge's axis, else its nearest
ancestor that does; an engine that cannot tell which node scrolls drags inside the node's own box. On the observation root: the screen's
main scrolling content, else what is under the viewport's center. The engine resolves once the container cannot scroll further toward
&lt;code class="paramref"&gt;Edge&lt;/code&gt;, at once when it is there already or nothing scrolls along that axis, and stops when the operation's budget runs
out. Not a kind of SPI v1, so an engine lists it explicitly. Upstream's `scrollTo`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.ScrollTo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs)

## Constructors

<a id="E2E_Engine_LocatorAction_ScrollTo__ctor_"></a>
<a id="E2E_Engine_LocatorAction_ScrollTo__ctor_E2E_Engine_ScrollEdge_"></a>

### ScrollTo(ScrollEdge)


```csharp
public ScrollTo(ScrollEdge Edge)
```


Scrolls a container to one of its edges, as far as it goes. On a node: the node when it scrolls along the edge's axis, else its nearest
ancestor that does; an engine that cannot tell which node scrolls drags inside the node's own box. On the observation root: the screen's
main scrolling content, else what is under the viewport's center. The engine resolves once the container cannot scroll further toward
&lt;code class="paramref"&gt;Edge&lt;/code&gt;, at once when it is there already or nothing scrolls along that axis, and stops when the operation's budget runs
out. Not a kind of SPI v1, so an engine lists it explicitly. Upstream's `scrollTo`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Edge` | [ScrollEdge](/api/E2E.Engine/ScrollEdge/) |  |

## Properties

<a id="E2E_Engine_LocatorAction_ScrollTo_Edge_"></a>
<a id="E2E_Engine_LocatorAction_ScrollTo_Edge"></a>

### Edge


```csharp
public ScrollEdge Edge { get; init; }
```


**Value:** [ScrollEdge](/api/E2E.Engine/ScrollEdge/)
