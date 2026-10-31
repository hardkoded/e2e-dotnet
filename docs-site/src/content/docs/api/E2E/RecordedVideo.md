---
title: "RecordedVideo"
description: "One video StopRecordingAsync(CancellationToken) returns: a file, or a link to a recording a hosted service keeps (upstream's RecordedVideo)."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedVideo"
  hidden: true
---

<a id="E2E_RecordedVideo"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public abstract record RecordedVideo : IEquatable<RecordedVideo>
```


One video [StopRecordingAsync(CancellationToken)](/api/E2E/App/#E2E_App_StopRecordingAsync_System_Threading_CancellationToken_) returns: a file, or a link to a recording a hosted service keeps (upstream's `RecordedVideo`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RecordedVideo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [RecordedVideoFile](/api/E2E/RecordedVideoFile/), [RecordedVideoLink](/api/E2E/RecordedVideoLink/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L22)

## Properties

<a id="E2E_RecordedVideo_StartedAt_"></a>
<a id="E2E_RecordedVideo_StartedAt"></a>

### StartedAt


```csharp
public required string StartedAt { get; init; }
```


When it started recording, as an ISO timestamp.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L25)
