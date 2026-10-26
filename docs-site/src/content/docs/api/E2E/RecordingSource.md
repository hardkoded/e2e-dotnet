---
title: "RecordingSource"
description: "Where an effective RecordingMode was set. Upstream RecordingSource. A Run mode (the config root or the environment variable) applies to every engine that can..."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordingSource"
  hidden: true
---

<a id="E2E_RecordingSource"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum RecordingSource
```


Where an effective [RecordingMode](/api/E2E/RecordingMode/) was set. Upstream `RecordingSource`.
A [Run](#E2E_RecordingSource_Run) mode (the config root or the environment variable) applies to every engine that can record.
A [Target](#E2E_RecordingSource_Target) or [Test](#E2E_RecordingSource_Test) mode was asked for by name, so the engine must be able to record it.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L35)

## Fields

<a id="E2E_RecordingSource_Default"></a>

### Default


```csharp
Default = 0
```


Nobody set it.

**Value:** [RecordingSource](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L38)

<a id="E2E_RecordingSource_Run"></a>

### Run


```csharp
Run = 1
```


The config root, or `E2E_TRACE` and `E2E_VIDEO`, the port's counterpart of `--trace` and `--video`.

**Value:** [RecordingSource](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L41)

<a id="E2E_RecordingSource_Target"></a>

### Target


```csharp
Target = 2
```


The target's own `trace` or `video` key.

**Value:** [RecordingSource](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L44)

<a id="E2E_RecordingSource_Test"></a>

### Test


```csharp
Test = 3
```


A setting made for one test.

**Value:** [RecordingSource](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L47)
