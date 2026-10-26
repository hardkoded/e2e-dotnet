---
title: "LocatorWaitForOptions"
description: "Options for CancellationToken)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorWaitForOptions"
  hidden: true
---

<a id="E2E_LocatorWaitForOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class LocatorWaitForOptions
```


Options for `CancellationToken)`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L984)

## Properties

<a id="E2E_LocatorWaitForOptions_State_"></a>
<a id="E2E_LocatorWaitForOptions_State"></a>

### State


```csharp
public WaitForState State { get; init; }
```


**Value:** [WaitForState](/api/E2E/WaitForState/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L986)

<a id="E2E_LocatorWaitForOptions_Timeout_"></a>
<a id="E2E_LocatorWaitForOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


How long to wait. Defaults to the configured action timeout.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L989)
