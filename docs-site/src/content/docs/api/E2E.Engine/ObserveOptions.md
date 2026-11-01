---
title: "ObserveOptions"
description: "What an observation takes besides the tree. Upstream's EngineObserveOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "ObserveOptions"
  hidden: true
---

<a id="E2E_Engine_ObserveOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ObserveOptions : IEquatable<ObserveOptions>
```


What an observation takes besides the tree. Upstream's `EngineObserveOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ObserveOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L158)

## Properties

<a id="E2E_Engine_ObserveOptions_Comparable_"></a>
<a id="E2E_Engine_ObserveOptions_Comparable"></a>

### Comparable


```csharp
public bool Comparable { get; init; }
```


Take the pixels in a state fit for comparing against a stored one: the status bar normalized.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L164)

<a id="E2E_Engine_ObserveOptions_Pixels_"></a>
<a id="E2E_Engine_ObserveOptions_Pixels"></a>

### Pixels


```csharp
public bool Pixels { get; init; }
```


Take the screen's pixels with the tree ([Pixels](/api/E2E.Engine/Observation/#E2E_Engine_Observation_Pixels)).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L161)
