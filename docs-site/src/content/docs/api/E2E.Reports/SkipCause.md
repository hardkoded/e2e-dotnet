---
title: "SkipCause"
description: "The cause of a skip."
editUrl: false
prev: false
next: false
sidebar:
  label: "SkipCause"
  hidden: true
---

<a id="E2E_Reports_SkipCause"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum SkipCause
```


The cause of a skip.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs)

## Fields

<a id="E2E_Reports_SkipCause_CapabilityUnavailable"></a>

### CapabilityUnavailable


```csharp
CapabilityUnavailable = 4
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_Explicit"></a>

### Explicit


```csharp
Explicit = 0
```


The test called `Skip`.

**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_FailureLimit"></a>

### FailureLimit


```csharp
FailureLimit = 9
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_Filtered"></a>

### Filtered


```csharp
Filtered = 2
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_Fixme"></a>

### Fixme


```csharp
Fixme = 1
```


The test was marked fixme (upstream's `test.fixme`); the reason is `fixme` or `fixme: <reason>`.

**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_HookFailed"></a>

### HookFailed


```csharp
HookFailed = 7
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_InfrastructureUnavailable"></a>

### InfrastructureUnavailable


```csharp
InfrastructureUnavailable = 8
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_PlatformUnavailable"></a>

### PlatformUnavailable


```csharp
PlatformUnavailable = 3
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_SerialPredecessorFailed"></a>

### SerialPredecessorFailed


```csharp
SerialPredecessorFailed = 6
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_SetupFailed"></a>

### SetupFailed


```csharp
SetupFailed = 5
```


**Value:** [SkipCause](./)

<a id="E2E_Reports_SkipCause_TimeLimit"></a>

### TimeLimit


```csharp
TimeLimit = 10
```


**Value:** [SkipCause](./)
