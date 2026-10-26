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

**Derived:** [PressSequentiallyOptions](/api/E2E/PressSequentiallyOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L938)

## Properties

<a id="E2E_ActionOptions_Timeout_"></a>
<a id="E2E_ActionOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


How long to wait for exactly one enabled match. Defaults to the configured action timeout.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L941)
