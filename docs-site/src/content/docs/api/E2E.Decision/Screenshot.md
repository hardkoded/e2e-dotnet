---
title: "Screenshot"
description: "Pixels as the decision model receives them: the runner's capture without its masking count (upstream's Screenshot, Omit<ExecutorPixels, 'maskedRegionCount'>)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Screenshot"
  hidden: true
---

<a id="E2E_Decision_Screenshot"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Screenshot : IEquatable<Screenshot>
```


Pixels as the decision model receives them: the runner's capture without its masking count (upstream's `Screenshot`, `Omit<ExecutorPixels, 'maskedRegionCount'>`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Screenshot&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Overlay.cs)

## Constructors

<a id="E2E_Decision_Screenshot__ctor_"></a>
<a id="E2E_Decision_Screenshot__ctor_System_Byte___System_String_System_Int32_System_Int32_System_Double_"></a>

### Screenshot(byte[], string, int, int, double)


```csharp
public Screenshot(byte[] Data, string MediaType, int Width, int Height, double Scale)
```


Pixels as the decision model receives them: the runner's capture without its masking count (upstream's `Screenshot`, `Omit<ExecutorPixels, 'maskedRegionCount'>`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The encoded image. |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The media type of &lt;code class="paramref"&gt;Data&lt;/code&gt;. |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Width in image pixels. |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Height in image pixels. |
| `Scale` | [double](https://learn.microsoft.com/dotnet/api/system.double) | Image pixels per viewport (CSS) pixel. |

## Properties

<a id="E2E_Decision_Screenshot_Data_"></a>
<a id="E2E_Decision_Screenshot_Data"></a>

### Data


```csharp
public byte[] Data { get; init; }
```


The encoded image.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

<a id="E2E_Decision_Screenshot_Height_"></a>
<a id="E2E_Decision_Screenshot_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


Height in image pixels.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Screenshot_MediaType_"></a>
<a id="E2E_Decision_Screenshot_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


The media type of &lt;code class="paramref"&gt;Data&lt;/code&gt;.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Screenshot_Scale_"></a>
<a id="E2E_Decision_Screenshot_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


Image pixels per viewport (CSS) pixel.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Screenshot_Width_"></a>
<a id="E2E_Decision_Screenshot_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


Width in image pixels.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
