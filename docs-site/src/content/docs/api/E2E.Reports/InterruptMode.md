---
title: "InterruptMode"
description: "How an interrupt landed: the running test is interrupted and its teardown runs, or (a second interrupt) every worker is torn down at once."
editUrl: false
prev: false
next: false
sidebar:
  label: "InterruptMode"
  hidden: true
---

<a id="E2E_Reports_InterruptMode"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum InterruptMode
```


How an interrupt landed: the running test is interrupted and its teardown runs, or (a second interrupt) every worker is torn down at once.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Fields

<a id="E2E_Reports_InterruptMode_Forced"></a>

### Forced


```csharp
Forced = 1
```


**Value:** [InterruptMode](./)

<a id="E2E_Reports_InterruptMode_Graceful"></a>

### Graceful


```csharp
Graceful = 0
```


**Value:** [InterruptMode](./)
