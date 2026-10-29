---
title: "E2EDefaults"
description: "The upstream defaults, in one place. E2EConfig, E2ESessionOptions, and the NUnit fixture all start from these values."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2EDefaults"
  hidden: true
---

<a id="E2E_E2EDefaults"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class E2EDefaults
```


The upstream defaults, in one place. [E2EConfig](/api/E2E/E2EConfig/), [E2ESessionOptions](/api/E2E/E2ESessionOptions/),
and the NUnit fixture all start from these values.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L11)

## Fields

<a id="E2E_E2EDefaults_ActionTimeout"></a>

### ActionTimeout


```csharp
public static readonly TimeSpan ActionTimeout
```


Upstream `actionTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L20)

<a id="E2E_E2EDefaults_AssertionTimeout"></a>

### AssertionTimeout


```csharp
public static readonly TimeSpan AssertionTimeout
```


Upstream `assertionTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L23)

<a id="E2E_E2EDefaults_CleanupTimeout"></a>

### CleanupTimeout


```csharp
public static readonly TimeSpan CleanupTimeout
```


Upstream `cleanupTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L26)

<a id="E2E_E2EDefaults_JudgmentTimeout"></a>

### JudgmentTimeout


```csharp
public static readonly TimeSpan JudgmentTimeout
```


Upstream `judgmentTimeout`: the deadline of one `assert`, `waitFor`, or `extract`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L32)

<a id="E2E_E2EDefaults_LaunchTimeout"></a>

### LaunchTimeout


```csharp
public static readonly TimeSpan LaunchTimeout
```


Upstream `launchTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L17)

<a id="E2E_E2EDefaults_MaxInputTokens"></a>

### MaxInputTokens


```csharp
public const int MaxInputTokens = 64000
```


Upstream's `DEFAULT_INPUT_TOKENS`, the default of `maxInputTokens`: the input one model request may carry.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L53)

<a id="E2E_E2EDefaults_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public const int MaxModelCalls = 25
```


Upstream `maxModelCalls` for one agent call.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L41)

<a id="E2E_E2EDefaults_MaxObservationBytes"></a>

### MaxObservationBytes


```csharp
public const int MaxObservationBytes = 262144
```


Upstream's `DEFAULT_OBSERVATION_BYTES`, the default of `maxObservationBytes`. A quarter mebibyte of tree is already tens of
thousands of tokens on every act turn; the per-call token ceiling clamps a dense screen below it.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L50)

<a id="E2E_E2EDefaults_MaxSteps"></a>

### MaxSteps


```csharp
public const int MaxSteps = 25
```


Upstream `maxSteps`: actions one `act` may take.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L44)

<a id="E2E_E2EDefaults_ReplayTimeout"></a>

### ReplayTimeout


```csharp
public static readonly TimeSpan ReplayTimeout
```


How long a replay waits for each recorded target and for the recorded end state.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L35)

<a id="E2E_E2EDefaults_StepTimeout"></a>

### StepTimeout


```csharp
public static readonly TimeSpan StepTimeout
```


How long one `act` may run. A .NET-only setting; upstream bounds `act` by the test timeout.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L29)

<a id="E2E_E2EDefaults_TestTimeout"></a>

### TestTimeout


```csharp
public static readonly TimeSpan TestTimeout
```


Upstream `timeout`: the whole test.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L14)

<a id="E2E_E2EDefaults_WaitForInterval"></a>

### WaitForInterval


```csharp
public static readonly TimeSpan WaitForInterval
```


How often `agent.waitFor` looks again.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EDefaults.cs#L38)
