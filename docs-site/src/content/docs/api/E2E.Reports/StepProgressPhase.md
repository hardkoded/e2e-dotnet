---
title: "StepProgressPhase"
description: "What a progress notification says (upstream's StepProgress['phase'])."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepProgressPhase"
  hidden: true
---

<a id="E2E_Reports_StepProgressPhase"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum StepProgressPhase
```


What a progress notification says (upstream's `StepProgress['phase']`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L8)

## Fields

<a id="E2E_Reports_StepProgressPhase_Activity"></a>

### Activity


```csharp
Activity = 3
```


What the running step waits on right now. Emitted as each begins; the matching event reports its end.

**Value:** [StepProgressPhase](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L15)

<a id="E2E_Reports_StepProgressPhase_End"></a>

### End


```csharp
End = 1
```


**Value:** [StepProgressPhase](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L11)

<a id="E2E_Reports_StepProgressPhase_Event"></a>

### Event


```csharp
Event = 2
```


**Value:** [StepProgressPhase](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L12)

<a id="E2E_Reports_StepProgressPhase_Replay"></a>

### Replay


```csharp
Replay = 4
```


The trace cache took the step ([Active](/api/E2E.Reports/StepProgress/#E2E_Reports_StepProgress_Active)), or handed it to the model after a replay that could not finish it.

**Value:** [StepProgressPhase](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L18)

<a id="E2E_Reports_StepProgressPhase_Start"></a>

### Start


```csharp
Start = 0
```


**Value:** [StepProgressPhase](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L10)
