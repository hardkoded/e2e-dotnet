---
title: "EngineSnapshot"
description: "Upstream's EngineSnapshot (engine/index.ts): what an engine's observe returns. A snapshot with TreeUnavailable carries only the root reference and the pixels."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineSnapshot"
  hidden: true
---

<a id="E2E_Engine_EngineSnapshot"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineSnapshot : IEquatable<EngineSnapshot>
```


Upstream's `EngineSnapshot` (`engine/index.ts`): what an engine's `observe` returns. A snapshot with
[TreeUnavailable](#E2E_Engine_EngineSnapshot_TreeUnavailable) carries only the root reference and the pixels.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineSnapshot&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_EngineSnapshot__ctor_"></a>
<a id="E2E_Engine_EngineSnapshot__ctor_E2E_Engine_EngineNode_E2E_Reports_ViewportSize_"></a>

### EngineSnapshot(EngineNode, ViewportSize)


```csharp
public EngineSnapshot(EngineNode Root, ViewportSize Viewport)
```


Upstream's `EngineSnapshot` (`engine/index.ts`): what an engine's `observe` returns. A snapshot with
[TreeUnavailable](#E2E_Engine_EngineSnapshot_TreeUnavailable) carries only the root reference and the pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Root` | [EngineNode](/api/E2E.Engine/EngineNode/) |  |
| `Viewport` | [ViewportSize](/api/E2E.Reports/ViewportSize/) |  |

## Properties

<a id="E2E_Engine_EngineSnapshot_KeyboardVisible_"></a>
<a id="E2E_Engine_EngineSnapshot_KeyboardVisible"></a>

### KeyboardVisible


```csharp
public bool? KeyboardVisible { get; init; }
```


Whether an on-screen keyboard is showing, when the engine measured it.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineSnapshot_Location_"></a>
<a id="E2E_Engine_EngineSnapshot_Location"></a>

### Location


```csharp
public string? Location { get; init; }
```


Where the surface was when captured, when the platform has a location.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineSnapshot_MaskedRegionCount_"></a>
<a id="E2E_Engine_EngineSnapshot_MaskedRegionCount"></a>

### MaskedRegionCount


```csharp
public int? MaskedRegionCount { get; init; }
```


How many regions the engine masked over the secure nodes it saw.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EngineSnapshot_Pixels_"></a>
<a id="E2E_Engine_EngineSnapshot_Pixels"></a>

### Pixels


```csharp
public ObservationPixels? Pixels { get; init; }
```


The screen's pixels, when the engine took them.

**Value:** [ObservationPixels](/api/E2E.Engine/ObservationPixels/)

<a id="E2E_Engine_EngineSnapshot_Root_"></a>
<a id="E2E_Engine_EngineSnapshot_Root"></a>

### Root


```csharp
public EngineNode Root { get; init; }
```


**Value:** [EngineNode](/api/E2E.Engine/EngineNode/)

<a id="E2E_Engine_EngineSnapshot_Tabs_"></a>
<a id="E2E_Engine_EngineSnapshot_Tabs"></a>

### Tabs


```csharp
public EngineTabs? Tabs { get; init; }
```


The tabs the surface has open, when it has more than one, such as a browser with a tab the app opened. The harness tells the model which
tab it reads and how many there are, and the agent switches between them through [SwitchTab](/api/E2E.Engine/EngineSessionSpec/#E2E_Engine_EngineSessionSpec_SwitchTab) and closes
them through [CloseTab](/api/E2E.Engine/EngineSessionSpec/#E2E_Engine_EngineSessionSpec_CloseTab). Null with one tab.

**Value:** [EngineTabs](/api/E2E.Engine/EngineTabs/)

<a id="E2E_Engine_EngineSnapshot_TreeUnavailable_"></a>
<a id="E2E_Engine_EngineSnapshot_TreeUnavailable"></a>

### TreeUnavailable


```csharp
public bool TreeUnavailable { get; init; }
```


The semantic tree could not be read; [Pixels](#E2E_Engine_EngineSnapshot_Pixels) are the only evidence of the screen.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineSnapshot_Truncated_"></a>
<a id="E2E_Engine_EngineSnapshot_Truncated"></a>

### Truncated


```csharp
public bool Truncated { get; init; }
```


True when the engine left nodes out of the tree to stay within its budget.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineSnapshot_Viewport_"></a>
<a id="E2E_Engine_EngineSnapshot_Viewport"></a>

### Viewport


```csharp
public ViewportSize Viewport { get; init; }
```


**Value:** [ViewportSize](/api/E2E.Reports/ViewportSize/)
