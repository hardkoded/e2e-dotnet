---
title: "WirePixels"
description: "Observation pixels on the wire: Data is base64. Upstream's WirePixels."
editUrl: false
prev: false
next: false
sidebar:
  label: "WirePixels"
  hidden: true
---

<a id="E2E_Engine_Protocol_WirePixels"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WirePixels : IEquatable<WirePixels>
```


Observation pixels on the wire: `Data` is base64. Upstream's `WirePixels`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WirePixels&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WirePixels__ctor_"></a>
<a id="E2E_Engine_Protocol_WirePixels__ctor_System_String_System_String_System_Int32_System_Int32_System_Double_"></a>

### WirePixels(string, string, int, int, double)


```csharp
public WirePixels(string Data, string MediaType, int Width, int Height, double Scale)
```


Observation pixels on the wire: `Data` is base64. Upstream's `WirePixels`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Scale` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Engine_Protocol_WirePixels_Data_"></a>
<a id="E2E_Engine_Protocol_WirePixels_Data"></a>

### Data


```csharp
public string Data { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WirePixels_Height_"></a>
<a id="E2E_Engine_Protocol_WirePixels_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_WirePixels_MediaType_"></a>
<a id="E2E_Engine_Protocol_WirePixels_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WirePixels_Scale_"></a>
<a id="E2E_Engine_Protocol_WirePixels_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Engine_Protocol_WirePixels_Width_"></a>
<a id="E2E_Engine_Protocol_WirePixels_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
