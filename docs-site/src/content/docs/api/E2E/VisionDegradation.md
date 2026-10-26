---
title: "VisionDegradation"
description: "Why a vision call fell back to tree-only input. Pixel evidence degrades rather than fails, except under Only, which fails with POLICYDENIED naming the reason."
editUrl: false
prev: false
next: false
sidebar:
  label: "VisionDegradation"
  hidden: true
---

<a id="E2E_VisionDegradation"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum VisionDegradation
```


Why a vision call fell back to tree-only input. Pixel evidence degrades rather than fails, except under
[Only](/api/E2E/VisionMode/#E2E_VisionMode_Only), which fails with `POLICY_DENIED` naming the reason.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L13)

## Fields

<a id="E2E_VisionDegradation_MaskingUnproven"></a>

### MaskingUnproven


```csharp
MaskingUnproven = 1
```


The engine masked fewer regions than the tree has secure nodes, so the image cannot be proven redacted.

**Value:** [VisionDegradation](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L19)

<a id="E2E_VisionDegradation_PixelTainted"></a>

### PixelTainted


```csharp
PixelTainted = 0
```


A secret was filled in this attempt, so the app may show it outside a secure field and no pixels reach a model.

**Value:** [VisionDegradation](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L16)

<a id="E2E_VisionDegradation_UnsupportedCapability"></a>

### UnsupportedCapability


```csharp
UnsupportedCapability = 2
```


The engine captures no pixels.

**Value:** [VisionDegradation](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Steps.cs#L22)
