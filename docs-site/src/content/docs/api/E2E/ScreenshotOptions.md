---
title: "ScreenshotOptions"
description: "Options of ToHaveScreenshotAsync."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScreenshotOptions"
  hidden: true
---

<a id="E2E_ScreenshotOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ScreenshotOptions
```


Options of `ToHaveScreenshotAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L8)

## Properties

<a id="E2E_ScreenshotOptions_Mask_"></a>
<a id="E2E_ScreenshotOptions_Mask"></a>

### Mask


```csharp
public IReadOnlyList<Locator>? Mask { get; init; }
```


Locators painted over before comparing, for content that changes from run to run.

**Value:** [IReadOnlyList&lt;Locator&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L20)

<a id="E2E_ScreenshotOptions_MaskColor_"></a>
<a id="E2E_ScreenshotOptions_MaskColor"></a>

### MaskColor


```csharp
public string? MaskColor { get; init; }
```


Color of the [Mask](#E2E_ScreenshotOptions_Mask) boxes, written `#rrggbb`. Default `#ff00ff`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L23)

<a id="E2E_ScreenshotOptions_MaxDiffPixelRatio_"></a>
<a id="E2E_ScreenshotOptions_MaxDiffPixelRatio"></a>

### MaxDiffPixelRatio


```csharp
public double? MaxDiffPixelRatio { get; init; }
```


What share of the pixels may differ, from 0 to 1. Default 0. With [MaxDiffPixels](#E2E_ScreenshotOptions_MaxDiffPixels) also set, the smaller count wins.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L17)

<a id="E2E_ScreenshotOptions_MaxDiffPixels_"></a>
<a id="E2E_ScreenshotOptions_MaxDiffPixels"></a>

### MaxDiffPixels


```csharp
public int? MaxDiffPixels { get; init; }
```


How many pixels may differ. Default 0.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L14)

<a id="E2E_ScreenshotOptions_Threshold_"></a>
<a id="E2E_ScreenshotOptions_Threshold"></a>

### Threshold


```csharp
public double? Threshold { get; init; }
```


How far one pixel's color may drift and still count as the same, from 0 (exact) to 1. Default 0.2.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L11)

<a id="E2E_ScreenshotOptions_Timeout_"></a>
<a id="E2E_ScreenshotOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Assertion budget. Defaults to the assertion timeout.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/ScreenshotOptions.cs#L26)
