---
title: "LocatorAction.MultiTap"
description: "Count taps in a row on one point: 3 or more, since 2 is LocatorAction.DoubleTap. A browser reads them as one multi-click (a triple click selects a paragraph)..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.MultiTap"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_MultiTap"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.MultiTap : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.MultiTap>
```


&lt;code class="paramref"&gt;Count&lt;/code&gt; taps in a row on one point: 3 or more, since 2 is [LocatorAction.DoubleTap](/api/E2E.Engine/LocatorAction.DoubleTap/). A browser reads them as one multi-click (a
triple click selects a paragraph); a device presses them at its own cadence, which a gesture detector may not read as one. Not a kind of SPI v1,
so an engine lists it explicitly. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for every click, as for [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/). Upstream's `multiTap`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.MultiTap&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs)

## Constructors

<a id="E2E_Engine_LocatorAction_MultiTap__ctor_"></a>
<a id="E2E_Engine_LocatorAction_MultiTap__ctor_System_Int64_System_Collections_Generic_IReadOnlyList_E2E_Engine_KeyModifier__"></a>

### `MultiTap(long, IReadOnlyList<KeyModifier>?)`


```csharp
public MultiTap(long Count, IReadOnlyList<KeyModifier>? Modifiers = null)
```


&lt;code class="paramref"&gt;Count&lt;/code&gt; taps in a row on one point: 3 or more, since 2 is [LocatorAction.DoubleTap](/api/E2E.Engine/LocatorAction.DoubleTap/). A browser reads them as one multi-click (a
triple click selects a paragraph); a device presses them at its own cadence, which a gesture detector may not read as one. Not a kind of SPI v1,
so an engine lists it explicitly. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for every click, as for [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/). Upstream's `multiTap`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Count` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |
| `Modifiers` | [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Engine_LocatorAction_MultiTap_Count_"></a>
<a id="E2E_Engine_LocatorAction_MultiTap_Count"></a>

### Count


```csharp
public long Count { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

<a id="E2E_Engine_LocatorAction_MultiTap_Modifiers_"></a>
<a id="E2E_Engine_LocatorAction_MultiTap_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
```


**Value:** [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
