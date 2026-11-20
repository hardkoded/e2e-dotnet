---
title: "Zoom"
description: "A zoomed crop: the image, and where its top-left corner sits in the screenshot's CSS pixels (upstream's Zoom)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Zoom"
  hidden: true
---

<a id="E2E_Decision_Zoom"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Zoom : IEquatable<Zoom>
```


A zoomed crop: the image, and where its top-left corner sits in the screenshot's CSS pixels (upstream's `Zoom`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Zoom&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Overlay.cs)

## Constructors

<a id="E2E_Decision_Zoom__ctor_"></a>
<a id="E2E_Decision_Zoom__ctor_E2E_Decision_Screenshot_E2E_Engine_Point_System_Double_"></a>

### Zoom(Screenshot, Point, double)


```csharp
public Zoom(Screenshot Screenshot, Point Origin, double Factor)
```


A zoomed crop: the image, and where its top-left corner sits in the screenshot's CSS pixels (upstream's `Zoom`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `Origin` | [Point](/api/E2E.Engine/Point/) |  |
| `Factor` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Decision_Zoom_Factor_"></a>
<a id="E2E_Decision_Zoom_Factor"></a>

### Factor


```csharp
public double Factor { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Zoom_Origin_"></a>
<a id="E2E_Decision_Zoom_Origin"></a>

### Origin


```csharp
public Point Origin { get; init; }
```


**Value:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_Decision_Zoom_Screenshot_"></a>
<a id="E2E_Decision_Zoom_Screenshot"></a>

### Screenshot


```csharp
public Screenshot Screenshot { get; init; }
```


**Value:** [Screenshot](/api/E2E.Decision/Screenshot/)
