---
title: "ReplayHandOffReason"
description: "Upstream's ReplayHandOffReason, why a cached replay stopped before it finished its trace. ActionBudget and InvalidEntry are the port's: its replay draws on t..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReplayHandOffReason"
  hidden: true
---

<a id="E2E_ReplayHandOffReason"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ReplayHandOffReason
```


Upstream's `ReplayHandOffReason`, why a cached replay stopped before it finished its trace. [ActionBudget](#E2E_ReplayHandOffReason_ActionBudget)
and [InvalidEntry](#E2E_ReplayHandOffReason_InvalidEntry) are the port's: its replay draws on the step's action budget, and reads a recording back
from a store that can hold any shape.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L508)

## Fields

<a id="E2E_ReplayHandOffReason_ActionBudget"></a>

### ActionBudget


```csharp
ActionBudget = 6
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L516)

<a id="E2E_ReplayHandOffReason_ActionFailed"></a>

### ActionFailed


```csharp
ActionFailed = 3
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L513)

<a id="E2E_ReplayHandOffReason_ActionUncertain"></a>

### ActionUncertain


```csharp
ActionUncertain = 4
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L514)

<a id="E2E_ReplayHandOffReason_EndMismatch"></a>

### EndMismatch


```csharp
EndMismatch = 5
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L515)

<a id="E2E_ReplayHandOffReason_Gap"></a>

### Gap


```csharp
Gap = 0
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L510)

<a id="E2E_ReplayHandOffReason_InvalidEntry"></a>

### InvalidEntry


```csharp
InvalidEntry = 7
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L517)

<a id="E2E_ReplayHandOffReason_TargetAmbiguous"></a>

### TargetAmbiguous


```csharp
TargetAmbiguous = 2
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L512)

<a id="E2E_ReplayHandOffReason_TargetNotFound"></a>

### TargetNotFound


```csharp
TargetNotFound = 1
```


**Value:** [ReplayHandOffReason](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L511)
