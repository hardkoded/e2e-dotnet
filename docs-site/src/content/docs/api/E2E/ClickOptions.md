---
title: "ClickOptions"
description: "What a tap-family action takes. Upstream's ClickOptions, of which the port has the modifiers."
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
public sealed class ClickOptions
```


What a tap-family action takes. Upstream's `ClickOptions`, of which the port has the `modifiers`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L330)

## Properties

<a id="E2E_ClickOptions_Modifiers_"></a>
<a id="E2E_ClickOptions_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
```


Keys held for the click, distinct. An empty list holds nothing.

**Value:** [IReadOnlyList&lt;KeyModifier&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L333)
