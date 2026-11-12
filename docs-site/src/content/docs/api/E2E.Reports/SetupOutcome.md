---
title: "SetupOutcome"
description: "How a setup step ended other than plainly done (upstream's SetupOutcome): Reused when a service or app command attached to a process already serving its URL..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SetupOutcome"
  hidden: true
---

<a id="E2E_Reports_SetupOutcome"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum SetupOutcome
```


How a setup step ended other than plainly done (upstream's `SetupOutcome`): `Reused` when a service or app command attached to a process
already serving its URL instead of spawning; `Failed` and `TimedOut` for a test filter (upstream's `FilterOutcome`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Fields

<a id="E2E_Reports_SetupOutcome_Failed"></a>

### Failed


```csharp
Failed = 1
```


**Value:** [SetupOutcome](./)

<a id="E2E_Reports_SetupOutcome_Reused"></a>

### Reused


```csharp
Reused = 0
```


**Value:** [SetupOutcome](./)

<a id="E2E_Reports_SetupOutcome_TimedOut"></a>

### TimedOut


```csharp
TimedOut = 2
```


**Value:** [SetupOutcome](./)
