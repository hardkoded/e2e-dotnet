---
title: "Observation"
description: "A redacted semantic snapshot of the current screen."
editUrl: false
prev: false
next: false
sidebar:
  label: "Observation"
  hidden: true
---

<a id="E2E_Engine_Observation"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class Observation
```


A redacted semantic snapshot of the current screen.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L112)

## Properties

<a id="E2E_Engine_Observation_KeyboardVisible_"></a>
<a id="E2E_Engine_Observation_KeyboardVisible"></a>

### KeyboardVisible


```csharp
public bool? KeyboardVisible { get; init; }
```


Whether an on-screen keyboard is showing, as the engine measured it apart from the tree: false when it measured none,
null only when it did not measure it.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L125)

<a id="E2E_Engine_Observation_Revision_"></a>
<a id="E2E_Engine_Observation_Revision"></a>

### Revision


```csharp
public string Revision { get; }
```


Which capture of the attempt this is (upstream's `revision`: `b1`, `b2`, and so on). The harness stamps it, not the
engine, as the observation reaches the attempt's agent; it reads empty before then. A step's report names the revision of the
screen it last read.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L138)

<a id="E2E_Engine_Observation_Roots_"></a>
<a id="E2E_Engine_Observation_Roots"></a>

### Roots


```csharp
public required IReadOnlyList<SemanticNode> Roots { get; init; }
```


**Value:** [IReadOnlyList&lt;SemanticNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L116)

<a id="E2E_Engine_Observation_Route_"></a>
<a id="E2E_Engine_Observation_Route"></a>

### Route


```csharp
public required string Route { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L114)

<a id="E2E_Engine_Observation_Truncated_"></a>
<a id="E2E_Engine_Observation_Truncated"></a>

### Truncated


```csharp
public bool Truncated { get; init; }
```


True when the engine left nodes out to stay within its node budget. Scrolling brings them in.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L119)
