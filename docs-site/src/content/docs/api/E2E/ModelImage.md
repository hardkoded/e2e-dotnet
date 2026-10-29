---
title: "ModelImage"
description: "An image a model reads: the encoded bytes of a screenshot, already sized for the model."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelImage"
  hidden: true
---

<a id="E2E_ModelImage"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ModelImage : IEquatable<ModelImage>
```


An image a model reads: the encoded bytes of a screenshot, already sized for the model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelImage&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L77)

## Constructors

<a id="E2E_ModelImage__ctor_"></a>
<a id="E2E_ModelImage__ctor_System_Byte___System_String_System_Int32_System_Int32_"></a>

### ModelImage(byte[], string, int, int)


```csharp
public ModelImage(byte[] Data, string MediaType, int Width, int Height)
```


An image a model reads: the encoded bytes of a screenshot, already sized for the model.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The encoded image. |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The media type of &lt;code class="paramref"&gt;Data&lt;/code&gt;, such as `image/png`. |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Width in image pixels. |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Height in image pixels. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L77)

## Properties

<a id="E2E_ModelImage_Data_"></a>
<a id="E2E_ModelImage_Data"></a>

### Data


```csharp
public byte[] Data { get; init; }
```


The encoded image.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L77)

<a id="E2E_ModelImage_Height_"></a>
<a id="E2E_ModelImage_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


Height in image pixels.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L77)

<a id="E2E_ModelImage_MediaType_"></a>
<a id="E2E_ModelImage_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


The media type of &lt;code class="paramref"&gt;Data&lt;/code&gt;, such as `image/png`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L77)

<a id="E2E_ModelImage_Width_"></a>
<a id="E2E_ModelImage_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


Width in image pixels.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L77)
