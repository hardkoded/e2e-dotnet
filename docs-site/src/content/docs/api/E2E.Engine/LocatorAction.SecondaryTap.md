---
title: "LocatorAction.SecondaryTap"
description: "A right click, or the platform's equivalent. <code class=\"paramref\">Modifiers</code> are held for the click, as for LocatorAction.Tap."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.SecondaryTap"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_SecondaryTap"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.SecondaryTap : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.SecondaryTap>
```


A right click, or the platform's equivalent. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for the click, as for [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.SecondaryTap&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L21)

## Constructors

<a id="E2E_Engine_LocatorAction_SecondaryTap__ctor_"></a>
<a id="E2E_Engine_LocatorAction_SecondaryTap__ctor_System_Collections_Generic_IReadOnlyList_E2E_Engine_KeyModifier__"></a>

### `SecondaryTap(IReadOnlyList<KeyModifier>?)`


```csharp
public SecondaryTap(IReadOnlyList<KeyModifier>? Modifiers = null)
```


A right click, or the platform's equivalent. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for the click, as for [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Modifiers` | [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L21)

## Properties

<a id="E2E_Engine_LocatorAction_SecondaryTap_Modifiers_"></a>
<a id="E2E_Engine_LocatorAction_SecondaryTap_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
```


**Value:** [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L21)
