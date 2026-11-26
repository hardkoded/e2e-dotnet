---
title: "WireSnapshot"
description: "An engine snapshot on the wire: the snapshot with its pixels as base64. Upstream's WireSnapshot."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireSnapshot"
  hidden: true
---

<a id="E2E_Engine_Protocol_WireSnapshot"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WireSnapshot : IEquatable<WireSnapshot>
```


An engine snapshot on the wire: the snapshot with its pixels as base64. Upstream's `WireSnapshot`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WireSnapshot&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WireSnapshot__ctor_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot__ctor_E2E_Engine_EngineNode_E2E_Reports_ViewportSize_"></a>

### WireSnapshot(EngineNode, ViewportSize)


```csharp
public WireSnapshot(EngineNode Root, ViewportSize Viewport)
```


An engine snapshot on the wire: the snapshot with its pixels as base64. Upstream's `WireSnapshot`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Root` | [EngineNode](/api/E2E.Engine/EngineNode/) |  |
| `Viewport` | [ViewportSize](/api/E2E.Reports/ViewportSize/) |  |

## Properties

<a id="E2E_Engine_Protocol_WireSnapshot_KeyboardVisible_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_KeyboardVisible"></a>

### KeyboardVisible


```csharp
public bool? KeyboardVisible { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_WireSnapshot_Location_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_Location"></a>

### Location


```csharp
public string? Location { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WireSnapshot_MaskedRegionCount_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_MaskedRegionCount"></a>

### MaskedRegionCount


```csharp
public int? MaskedRegionCount { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_WireSnapshot_Pixels_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_Pixels"></a>

### Pixels


```csharp
public WirePixels? Pixels { get; init; }
```


**Value:** [WirePixels](/api/E2E.Engine.Protocol/WirePixels/)

<a id="E2E_Engine_Protocol_WireSnapshot_Root_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_Root"></a>

### Root


```csharp
public EngineNode Root { get; init; }
```


**Value:** [EngineNode](/api/E2E.Engine/EngineNode/)

<a id="E2E_Engine_Protocol_WireSnapshot_Tabs_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_Tabs"></a>

### Tabs


```csharp
public EngineTabs? Tabs { get; init; }
```


**Value:** [EngineTabs](/api/E2E.Engine/EngineTabs/)

<a id="E2E_Engine_Protocol_WireSnapshot_TreeUnavailable_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_TreeUnavailable"></a>

### TreeUnavailable


```csharp
public bool TreeUnavailable { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_WireSnapshot_Truncated_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_Truncated"></a>

### Truncated


```csharp
public bool Truncated { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_WireSnapshot_Viewport_"></a>
<a id="E2E_Engine_Protocol_WireSnapshot_Viewport"></a>

### Viewport


```csharp
public ViewportSize Viewport { get; init; }
```


**Value:** [ViewportSize](/api/E2E.Reports/ViewportSize/)
