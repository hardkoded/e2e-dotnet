---
title: "ScrollUntilVisibleOptions"
description: "Options for Screen.ScrollUntilVisibleAsync and Locator.ScrollUntilVisibleAsync."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScrollUntilVisibleOptions"
  hidden: true
---

<a id="E2E_ScrollUntilVisibleOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ScrollUntilVisibleOptions
```


Options for `Screen.ScrollUntilVisibleAsync` and `Locator.ScrollUntilVisibleAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1011)

## Properties

<a id="E2E_ScrollUntilVisibleOptions_Direction_"></a>
<a id="E2E_ScrollUntilVisibleOptions_Direction"></a>

### Direction


```csharp
public ScrollDirection? Direction { get; init; }
```


Which way to scroll. Defaults to [Down](/api/E2E.Engine/ScrollDirection/#E2E_Engine_ScrollDirection_Down).

**Value:** `ScrollDirection?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1016)

<a id="E2E_ScrollUntilVisibleOptions_Momentum_"></a>
<a id="E2E_ScrollUntilVisibleOptions_Momentum"></a>

### Momentum


```csharp
public Momentum? Momentum { get; init; }
```


The stride of each step. Defaults to [Slow](/api/E2E.Engine/Momentum/#E2E_Engine_Momentum_Slow).

**Value:** `Momentum?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1019)

<a id="E2E_ScrollUntilVisibleOptions_Timeout_"></a>
<a id="E2E_ScrollUntilVisibleOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


How long to scroll before giving up, every swipe included. Defaults to 30 seconds.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1022)
