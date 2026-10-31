---
title: "VisionDegradation"
description: "Why a step's pixels were not trusted."
editUrl: false
prev: false
next: false
sidebar:
  label: "VisionDegradation"
  hidden: true
---

<a id="E2E_Reports_VisionDegradation"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum VisionDegradation
```


Why a step's pixels were not trusted.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L543)

## Fields

<a id="E2E_Reports_VisionDegradation_MaskingUnproven"></a>

### MaskingUnproven


```csharp
MaskingUnproven = 1
```


**Value:** [VisionDegradation](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L547)

<a id="E2E_Reports_VisionDegradation_PixelTainted"></a>

### PixelTainted


```csharp
PixelTainted = 0
```


Only on a result an earlier version wrote, such as one `--last-failed` carries. A step no longer degrades this way.

**Value:** [VisionDegradation](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L546)

<a id="E2E_Reports_VisionDegradation_UnsupportedCapability"></a>

### UnsupportedCapability


```csharp
UnsupportedCapability = 2
```


**Value:** [VisionDegradation](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L548)
