---
title: "ScreenPoint"
description: "Upstream's ScreenPoint: a viewport point in pixels, percentages, or both, new ScreenPoint(\"50%\", 120). A Point converts to one."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScreenPoint"
  hidden: true
---

<a id="E2E_ScreenPoint"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ScreenPoint : IEquatable<ScreenPoint>
```


Upstream's `ScreenPoint`: a viewport point in pixels, percentages, or both, `new ScreenPoint("50%", 120)`. A [Point](/api/E2E.Engine/Point/)
converts to one.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ScreenPoint&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs)

## Constructors

<a id="E2E_ScreenPoint__ctor_"></a>
<a id="E2E_ScreenPoint__ctor_E2E_ScreenCoordinate_E2E_ScreenCoordinate_"></a>

### ScreenPoint(ScreenCoordinate, ScreenCoordinate)


```csharp
public ScreenPoint(ScreenCoordinate X, ScreenCoordinate Y)
```


Upstream's `ScreenPoint`: a viewport point in pixels, percentages, or both, `new ScreenPoint("50%", 120)`. A [Point](/api/E2E.Engine/Point/)
converts to one.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `X` | [ScreenCoordinate](/api/E2E/ScreenCoordinate/) |  |
| `Y` | [ScreenCoordinate](/api/E2E/ScreenCoordinate/) |  |

## Properties

<a id="E2E_ScreenPoint_X_"></a>
<a id="E2E_ScreenPoint_X"></a>

### X


```csharp
public ScreenCoordinate X { get; init; }
```


**Value:** [ScreenCoordinate](/api/E2E/ScreenCoordinate/)

<a id="E2E_ScreenPoint_Y_"></a>
<a id="E2E_ScreenPoint_Y"></a>

### Y


```csharp
public ScreenCoordinate Y { get; init; }
```


**Value:** [ScreenCoordinate](/api/E2E/ScreenCoordinate/)

## Operators

<a id="E2E_ScreenPoint_op_Implicit_"></a>
<a id="E2E_ScreenPoint_op_Implicit_E2E_Engine_Point__E2E_ScreenPoint"></a>

### implicit operator ScreenPoint(Point)


```csharp
public static implicit operator ScreenPoint(Point point)
```


A point in CSS pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |

**Returns:** [ScreenPoint](./)
