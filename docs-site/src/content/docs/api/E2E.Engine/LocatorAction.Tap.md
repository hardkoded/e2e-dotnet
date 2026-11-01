---
title: "LocatorAction.Tap"
description: "A tap. Modifiers are held for the click, and only an engine that declares TapModifiers receives them."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.Tap"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_Tap"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.Tap : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.Tap>
```


A tap. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for the click, and only an engine that declares [TapModifiers](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_TapModifiers) receives them.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.Tap&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L15)

## Constructors

<a id="E2E_Engine_LocatorAction_Tap__ctor_"></a>
<a id="E2E_Engine_LocatorAction_Tap__ctor_System_Collections_Generic_IReadOnlyList_E2E_Engine_KeyModifier__"></a>

### `Tap(IReadOnlyList<KeyModifier>?)`


```csharp
public Tap(IReadOnlyList<KeyModifier>? Modifiers = null)
```


A tap. &lt;code class="paramref"&gt;Modifiers&lt;/code&gt; are held for the click, and only an engine that declares [TapModifiers](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_TapModifiers) receives them.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Modifiers` | [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L15)

## Properties

<a id="E2E_Engine_LocatorAction_Tap_Modifiers_"></a>
<a id="E2E_Engine_LocatorAction_Tap_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
```


**Value:** [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L15)
