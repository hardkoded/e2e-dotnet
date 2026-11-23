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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

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
| `Png` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The image, with every secure field covered. Empty for a withheld screenshot ([Withheld(int)](#E2E_Engine_EngineScreenshot_Withheld_System_Int32_)). |
| `Scale` | [double](https://learn.microsoft.com/dotnet/api/system.double) | Image pixels per viewport (CSS) pixel. |
| `MaskedRegionCount` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) | How many regions the engine covered, so a caller can prove the image is at least as redacted as the tree. Null when the engine cannot say. |

## Properties

<a id="E2E_Engine_EngineScreenshot_IsWithheld_"></a>
<a id="E2E_Engine_EngineScreenshot_IsWithheld"></a>

### IsWithheld


```csharp
public bool IsWithheld { get; }
```


Whether the engine withheld the image (see [Withheld(int)](#E2E_Engine_EngineScreenshot_Withheld_System_Int32_)): it reports the regions it masked and holds no pixels.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineScreenshot_MaskedRegionCount_"></a>
<a id="E2E_Engine_EngineScreenshot_MaskedRegionCount"></a>

### MaskedRegionCount


```csharp
public int? MaskedRegionCount { get; init; }
```


How many regions the engine covered, so a caller can prove the image is at least as redacted as the tree. Null when the engine cannot say.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EngineScreenshot_Png_"></a>
<a id="E2E_Engine_EngineScreenshot_Png"></a>

### Png


```csharp
public byte[] Png { get; init; }
```


The image, with every secure field covered. Empty for a withheld screenshot ([Withheld(int)](#E2E_Engine_EngineScreenshot_Withheld_System_Int32_)).

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

<a id="E2E_Engine_EngineScreenshot_Scale_"></a>
<a id="E2E_Engine_EngineScreenshot_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


Image pixels per viewport (CSS) pixel.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

## Methods

<a id="E2E_Engine_EngineScreenshot_Withheld_"></a>
<a id="E2E_Engine_EngineScreenshot_Withheld_System_Int32_"></a>

### Withheld(int)


```csharp
public static EngineScreenshot Withheld(int maskedRegionCount)
```


A screenshot the engine took and withheld because it could not mask every secure node on the screen (upstream's snapshot that
carries `maskedRegionCount` without `pixels`). It holds no image and reports the regions the engine could mask, fewer than
the screen's secure nodes, so `toHaveScreenshot` fails with `POLICY_DENIED` and the agent reads `MASKING_UNPROVEN`,
not a missing screenshot. Held as an empty [Png](#E2E_Engine_EngineScreenshot_Png) (.NET-only: upstream's optional `pixels`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `maskedRegionCount` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [EngineScreenshot](./)
