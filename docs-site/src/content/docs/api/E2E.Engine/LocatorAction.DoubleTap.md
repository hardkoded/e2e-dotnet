---
title: "LocatorAction.DoubleTap"
description: "A double tap. <code class=\"paramref\">Modifiers</code> are held for both clicks, as for LocatorAction.Tap."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.DoubleTap"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_DoubleTap"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.DoubleTap : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.DoubleTap>
```


A double tap. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for both clicks, as for [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.DoubleTap&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L18)

## Constructors

<a id="E2E_Engine_LocatorAction_DoubleTap__ctor_"></a>
<a id="E2E_Engine_LocatorAction_DoubleTap__ctor_System_Collections_Generic_IReadOnlyList_E2E_Engine_KeyModifier__"></a>

### `DoubleTap(IReadOnlyList<KeyModifier>?)`


```csharp
public DoubleTap(IReadOnlyList<KeyModifier>? Modifiers = null)
```


A double tap. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for both clicks, as for [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Modifiers` | [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L18)

## Properties

<a id="E2E_Engine_LocatorAction_DoubleTap_Modifiers_"></a>
<a id="E2E_Engine_LocatorAction_DoubleTap_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
```


**Value:** [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L18)
