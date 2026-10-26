---
title: "ExecutorPixels"
description: "Masked viewport pixels cleared for model input: the engine's capture, sized for the model, and its proven mask count."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorPixels"
  hidden: true
---

<a id="E2E_ExecutorPixels"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExecutorPixels : IEquatable<ExecutorPixels>
```


Masked viewport pixels cleared for model input: the engine's capture, sized for the model, and its proven mask count.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExecutorPixels&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

## Constructors

<a id="E2E_ExecutorPixels__ctor_"></a>
<a id="E2E_ExecutorPixels__ctor_System_Byte___System_String_System_Int32_System_Int32_System_Double_System_Int32_"></a>

### ExecutorPixels(byte[], string, int, int, double, int)


```csharp
public ExecutorPixels(byte[] Data, string MediaType, int Width, int Height, double Scale, int MaskedRegionCount)
```


Masked viewport pixels cleared for model input: the engine's capture, sized for the model, and its proven mask count.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The encoded image. |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The media type of &lt;code class="paramref"&gt;Data&lt;/code&gt;. |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Width in image pixels. |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Height in image pixels. |
| `Scale` | [double](https://learn.microsoft.com/dotnet/api/system.double) | Image pixels per viewport (CSS) pixel. |
| `MaskedRegionCount` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | How many regions the engine covered. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

## Properties

<a id="E2E_ExecutorPixels_Data_"></a>
<a id="E2E_ExecutorPixels_Data"></a>

### Data


```csharp
public byte[] Data { get; init; }
```


The encoded image.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

<a id="E2E_ExecutorPixels_Height_"></a>
<a id="E2E_ExecutorPixels_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


Height in image pixels.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

<a id="E2E_ExecutorPixels_MaskedRegionCount_"></a>
<a id="E2E_ExecutorPixels_MaskedRegionCount"></a>

### MaskedRegionCount


```csharp
public int MaskedRegionCount { get; init; }
```


How many regions the engine covered.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

<a id="E2E_ExecutorPixels_MediaType_"></a>
<a id="E2E_ExecutorPixels_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


The media type of &lt;code class="paramref"&gt;Data&lt;/code&gt;.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

<a id="E2E_ExecutorPixels_Scale_"></a>
<a id="E2E_ExecutorPixels_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


Image pixels per viewport (CSS) pixel.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)

<a id="E2E_ExecutorPixels_Width_"></a>
<a id="E2E_ExecutorPixels_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


Width in image pixels.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L298)
