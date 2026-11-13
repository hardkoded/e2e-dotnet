---
title: "SessionObservation"
description: "Upstream's Observation (engine/surface.ts) as the session returns it: a semantic capture with a Tree, or a pixel-only capture with Pixels and no tree."
editUrl: false
prev: false
next: false
sidebar:
  label: "SessionObservation"
  hidden: true
---

<a id="E2E_Engine_SessionObservation"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record SessionObservation : IEquatable<SessionObservation>
```


Upstream's `Observation` (`engine/surface.ts`) as the session returns it: a semantic capture with a [Tree](#E2E_Engine_SessionObservation_Tree), or a
pixel-only capture with [Pixels](#E2E_Engine_SessionObservation_Pixels) and no tree.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SessionObservation&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Properties

<a id="E2E_Engine_SessionObservation_CapturedAt_"></a>
<a id="E2E_Engine_SessionObservation_CapturedAt"></a>

### CapturedAt


```csharp
public required string CapturedAt { get; init; }
```


When the capture was taken, as an ISO timestamp.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_SessionObservation_KeyboardVisible_"></a>
<a id="E2E_Engine_SessionObservation_KeyboardVisible"></a>

### KeyboardVisible


```csharp
public bool? KeyboardVisible { get; init; }
```


Whether an on-screen keyboard is showing, when the engine measured it.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_SessionObservation_Kind_"></a>
<a id="E2E_Engine_SessionObservation_Kind"></a>

### Kind


```csharp
public required string Kind { get; init; }
```


The capture's kind: `semantic`, or `pixels` for a pixel-only capture.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_SessionObservation_Location_"></a>
<a id="E2E_Engine_SessionObservation_Location"></a>

### Location


```csharp
public string? Location { get; init; }
```


Where the surface was when captured, when the platform has a location.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_SessionObservation_Pixels_"></a>
<a id="E2E_Engine_SessionObservation_Pixels"></a>

### Pixels


```csharp
public ObservationPixels? Pixels { get; init; }
```


The screen's pixels, when the capture has them.

**Value:** [ObservationPixels](/api/E2E.Engine/ObservationPixels/)

<a id="E2E_Engine_SessionObservation_Redaction_"></a>
<a id="E2E_Engine_SessionObservation_Redaction"></a>

### Redaction


```csharp
public required ObservationRedaction Redaction { get; init; }
```


What the engine masked against the secure nodes it saw.

**Value:** [ObservationRedaction](/api/E2E.Engine/ObservationRedaction/)

<a id="E2E_Engine_SessionObservation_Revision_"></a>
<a id="E2E_Engine_SessionObservation_Revision"></a>

### Revision


```csharp
public required string Revision { get; init; }
```


The capture's revision, `b1`, `b2`, and so on.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_SessionObservation_Root_"></a>
<a id="E2E_Engine_SessionObservation_Root"></a>

### Root


```csharp
public required NodeRef Root { get; init; }
```


The viewport action reference, valid for this capture.

**Value:** [NodeRef](/api/E2E.Engine/NodeRef/)

<a id="E2E_Engine_SessionObservation_Tabs_"></a>
<a id="E2E_Engine_SessionObservation_Tabs"></a>

### Tabs


```csharp
public EngineTabs? Tabs { get; init; }
```


The open tabs, when more than one is ([Tabs](/api/E2E.Engine/EngineSnapshot/#E2E_Engine_EngineSnapshot_Tabs)).

**Value:** [EngineTabs](/api/E2E.Engine/EngineTabs/)

<a id="E2E_Engine_SessionObservation_Tree_"></a>
<a id="E2E_Engine_SessionObservation_Tree"></a>

### Tree


```csharp
public EngineNode? Tree { get; init; }
```


The semantic tree, with every node's ref stamped with this revision; null for a pixel-only capture.

**Value:** [EngineNode](/api/E2E.Engine/EngineNode/)

<a id="E2E_Engine_SessionObservation_Truncated_"></a>
<a id="E2E_Engine_SessionObservation_Truncated"></a>

### Truncated


```csharp
public bool Truncated { get; init; }
```


True when the tree was cut short to stay within the engine's budget.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_SessionObservation_Viewport_"></a>
<a id="E2E_Engine_SessionObservation_Viewport"></a>

### Viewport


```csharp
public required ViewportSize Viewport { get; init; }
```


The viewport the capture was taken in.

**Value:** [ViewportSize](/api/E2E.Reports/ViewportSize/)
