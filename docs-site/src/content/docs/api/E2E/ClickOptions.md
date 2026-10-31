---
title: "ClickOptions"
description: "Options for CancellationToken), CancellationToken), and CancellationToken). Upstream's ClickOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "ClickOptions"
  hidden: true
---

<a id="E2E_ClickOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class ClickOptions : ActionOptions
```


Options for `CancellationToken)`, `CancellationToken)`, and `CancellationToken)`. Upstream's `ClickOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionOptions](/api/E2E/ActionOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1222)

## Properties

<a id="E2E_ClickOptions_Modifiers_"></a>
<a id="E2E_ClickOptions_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
```


Keys held for the click, as a Shift-click extends a selection. Distinct; an empty list holds nothing. Only an engine that declares
[TapModifiers](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_TapModifiers) takes them.

**Value:** [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1228)
