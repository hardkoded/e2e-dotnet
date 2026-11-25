---
title: "PixelsWithheld"
description: "Upstream's PixelsWithheld (engine/surface.ts): why pixels a caller asked for are not part of an observation."
editUrl: false
prev: false
next: false
sidebar:
  label: "PixelsWithheld"
  hidden: true
---

<a id="E2E_Engine_PixelsWithheld"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public enum PixelsWithheld
```


Upstream's `PixelsWithheld` (`engine/surface.ts`): why pixels a caller asked for are not part of an observation.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Fields

<a id="E2E_Engine_PixelsWithheld_MaskingUnproven"></a>

### MaskingUnproven


```csharp
MaskingUnproven = 0
```


The masked regions are fewer than the secure nodes, so the image cannot be proven redacted (upstream's `MASKING_UNPROVEN`).

**Value:** [PixelsWithheld](./)
