---
title: "Momentum"
description: "Fling strength of a swipe: how far the gesture travels. Upstream's Momentum (none, slow, fast)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Momentum"
  hidden: true
---

<a id="E2E_Engine_Momentum"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public enum Momentum
```


Fling strength of a swipe: how far the gesture travels. Upstream's `Momentum` (`none`, `slow`, `fast`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L72)

## Fields

<a id="E2E_Engine_Momentum_Fast"></a>

### Fast


```csharp
Fast = 2
```


A long stride: four fifths of a node on a device, one and a half extents on the web.

**Value:** [Momentum](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L81)

<a id="E2E_Engine_Momentum_None"></a>

### None


```csharp
None = 0
```


No fling: half an extent on the web, half a node on a device.

**Value:** [Momentum](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L75)

<a id="E2E_Engine_Momentum_Slow"></a>

### Slow


```csharp
Slow = 1
```


A short stride. The engine decides how far: a node swipe on a device is a quarter of the node, on the web three quarters of its extent.

**Value:** [Momentum](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L78)
