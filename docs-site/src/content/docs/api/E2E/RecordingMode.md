---
title: "RecordingMode"
description: "Which attempts of a test record a trace or a video. Upstream RecordingMode. In e2e.config.json and in E2ETRACE and E2EVIDEO a mode is written off, on, retain..."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordingMode"
  hidden: true
---

<a id="E2E_RecordingMode"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum RecordingMode
```


Which attempts of a test record a trace or a video. Upstream `RecordingMode`. In
`e2e.config.json` and in `E2E_TRACE` and `E2E_VIDEO` a mode is written
`off`, `on`, `retain-on-failure`, `on-first-retry`, or `on-all-retries`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L12)

## Fields

<a id="E2E_RecordingMode_Off"></a>

### Off


```csharp
Off = 0
```


Record nothing.

**Value:** [RecordingMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L15)

<a id="E2E_RecordingMode_On"></a>

### On


```csharp
On = 1
```


Record every attempt and keep it.

**Value:** [RecordingMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L18)

<a id="E2E_RecordingMode_OnAllRetries"></a>

### OnAllRetries


```csharp
OnAllRetries = 4
```


Record every retry.

**Value:** [RecordingMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L27)

<a id="E2E_RecordingMode_OnFirstRetry"></a>

### OnFirstRetry


```csharp
OnFirstRetry = 3
```


Record the first retry only.

**Value:** [RecordingMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L24)

<a id="E2E_RecordingMode_RetainOnFailure"></a>

### RetainOnFailure


```csharp
RetainOnFailure = 2
```


Record every attempt and keep one that did not pass.

**Value:** [RecordingMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L21)
