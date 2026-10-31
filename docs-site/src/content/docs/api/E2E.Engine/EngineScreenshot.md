---
title: "EngineScreenshot"
description: "A captured viewport: PNG bytes, and how many image pixels one viewport (CSS) pixel takes."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineScreenshot"
  hidden: true
---

<a id="E2E_Engine_EngineScreenshot"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineScreenshot : IEquatable<EngineScreenshot>
```


A captured viewport: PNG bytes, and how many image pixels one viewport (CSS) pixel takes.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineScreenshot&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L293)

## Constructors

<a id="E2E_Engine_EngineScreenshot__ctor_"></a>
<a id="E2E_Engine_EngineScreenshot__ctor_System_Byte___System_Double_System_Nullable_System_Int32__"></a>

### EngineScreenshot(byte[], double, int?)


```csharp
public EngineScreenshot(byte[] Png, double Scale, int? MaskedRegionCount = null)
```


A captured viewport: PNG bytes, and how many image pixels one viewport (CSS) pixel takes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Png` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The image, with every secure field covered. |
| `Scale` | [double](https://learn.microsoft.com/dotnet/api/system.double) | Image pixels per viewport (CSS) pixel. |
| `MaskedRegionCount` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) | How many regions the engine covered, so a caller can prove the image is at least as redacted as the tree. Null when the engine cannot say. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L293)

## Properties

<a id="E2E_Engine_EngineScreenshot_MaskedRegionCount_"></a>
<a id="E2E_Engine_EngineScreenshot_MaskedRegionCount"></a>

### MaskedRegionCount


```csharp
public int? MaskedRegionCount { get; init; }
```


How many regions the engine covered, so a caller can prove the image is at least as redacted as the tree. Null when the engine cannot say.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L293)

<a id="E2E_Engine_EngineScreenshot_Png_"></a>
<a id="E2E_Engine_EngineScreenshot_Png"></a>

### Png


```csharp
public byte[] Png { get; init; }
```


The image, with every secure field covered.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L293)

<a id="E2E_Engine_EngineScreenshot_Scale_"></a>
<a id="E2E_Engine_EngineScreenshot_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


Image pixels per viewport (CSS) pixel.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L293)
