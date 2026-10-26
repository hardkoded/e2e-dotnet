---
title: "BoundingBox"
description: "A viewport-relative rectangle in CSS pixels."
editUrl: false
prev: false
next: false
sidebar:
  label: "BoundingBox"
  hidden: true
---

<a id="E2E_Engine_BoundingBox"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BoundingBox : IEquatable<BoundingBox>
```


A viewport-relative rectangle in CSS pixels.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BoundingBox&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L42)

## Constructors

<a id="E2E_Engine_BoundingBox__ctor_"></a>
<a id="E2E_Engine_BoundingBox__ctor_System_Double_System_Double_System_Double_System_Double_"></a>

### BoundingBox(double, double, double, double)


```csharp
public BoundingBox(double X, double Y, double Width, double Height)
```


A viewport-relative rectangle in CSS pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `X` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Y` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Width` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Height` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L42)

## Properties

<a id="E2E_Engine_BoundingBox_Height_"></a>
<a id="E2E_Engine_BoundingBox_Height"></a>

### Height


```csharp
public double Height { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L42)

<a id="E2E_Engine_BoundingBox_Width_"></a>
<a id="E2E_Engine_BoundingBox_Width"></a>

### Width


```csharp
public double Width { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L42)

<a id="E2E_Engine_BoundingBox_X_"></a>
<a id="E2E_Engine_BoundingBox_X"></a>

### X


```csharp
public double X { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L42)

<a id="E2E_Engine_BoundingBox_Y_"></a>
<a id="E2E_Engine_BoundingBox_Y"></a>

### Y


```csharp
public double Y { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L42)
