---
title: "ObservationPixels"
description: "Upstream's ObservationPixels: an encoded image of the screen with every secure field covered, and its geometry. Named apart from the port's EngineScreenshot,..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ObservationPixels"
  hidden: true
---

<a id="E2E_Engine_ObservationPixels"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ObservationPixels : IEquatable<ObservationPixels>
```


Upstream's `ObservationPixels`: an encoded image of the screen with every secure field covered, and its geometry. Named apart from
the port's [EngineScreenshot](/api/E2E.Engine/EngineScreenshot/), which carries no geometry.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ObservationPixels&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_ObservationPixels__ctor_"></a>
<a id="E2E_Engine_ObservationPixels__ctor_System_Byte___System_String_System_Int32_System_Int32_System_Double_"></a>

### ObservationPixels(byte[], string, int, int, double)


```csharp
public ObservationPixels(byte[] Data, string MediaType, int Width, int Height, double Scale)
```


Upstream's `ObservationPixels`: an encoded image of the screen with every secure field covered, and its geometry. Named apart from
the port's [EngineScreenshot](/api/E2E.Engine/EngineScreenshot/), which carries no geometry.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) |  |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Scale` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Engine_ObservationPixels_Data_"></a>
<a id="E2E_Engine_ObservationPixels_Data"></a>

### Data


```csharp
public byte[] Data { get; init; }
```


**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

<a id="E2E_Engine_ObservationPixels_Height_"></a>
<a id="E2E_Engine_ObservationPixels_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_ObservationPixels_MediaType_"></a>
<a id="E2E_Engine_ObservationPixels_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_ObservationPixels_Scale_"></a>
<a id="E2E_Engine_ObservationPixels_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Engine_ObservationPixels_Width_"></a>
<a id="E2E_Engine_ObservationPixels_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
