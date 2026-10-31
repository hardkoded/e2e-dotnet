---
title: "ActionOptions"
description: "Options for one locator action."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActionOptions"
  hidden: true
---

<a id="E2E_ActionOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class ActionOptions
```


Options for one locator action.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Derived:** [ClickOptions](/api/E2E/ClickOptions/), [LongPressOptions](/api/E2E/LongPressOptions/), [PressSequentiallyOptions](/api/E2E/PressSequentiallyOptions/), [SwipeOptions](/api/E2E/SwipeOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1215)

## Properties

<a id="E2E_ActionOptions_Timeout_"></a>
<a id="E2E_ActionOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


How long to wait for exactly one enabled match. Defaults to the configured action timeout.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1218)
