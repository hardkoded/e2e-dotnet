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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Fields

<a id="E2E_ReplayHandOffReason_ActionBudget"></a>

### ActionBudget


```csharp
ActionBudget = 7
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_ActionFailed"></a>

### ActionFailed


```csharp
ActionFailed = 4
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_ActionUncertain"></a>

### ActionUncertain


```csharp
ActionUncertain = 5
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_EndMismatch"></a>

### EndMismatch


```csharp
EndMismatch = 6
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_Gap"></a>

### Gap


```csharp
Gap = 0
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_InvalidEntry"></a>

### InvalidEntry


```csharp
InvalidEntry = 8
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_TargetAmbiguous"></a>

### TargetAmbiguous


```csharp
TargetAmbiguous = 2
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_TargetNotFound"></a>

### TargetNotFound


```csharp
TargetNotFound = 1
```


**Value:** [ReplayHandOffReason](./)

<a id="E2E_ReplayHandOffReason_ViewportChanged"></a>

### ViewportChanged


```csharp
ViewportChanged = 3
```


A recorded bare-point action met a viewport of another size; the point would land elsewhere.

**Value:** [ReplayHandOffReason](./)
