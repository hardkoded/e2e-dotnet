---
title: "RecordedVideoFile"
description: "A video file under the attempt's recordings/ directory (upstream's RecordedVideoFile)."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedVideoFile"
  hidden: true
---

<a id="E2E_RecordedVideoFile"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record RecordedVideoFile : RecordedVideo, IEquatable<RecordedVideo>, IEquatable<RecordedVideoFile>
```


A video file under the attempt's `recordings/` directory (upstream's `RecordedVideoFile`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RecordedVideo](/api/E2E/RecordedVideo/)

**Implements:** [IEquatable&lt;RecordedVideo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RecordedVideoFile&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L29)

## Properties

<a id="E2E_RecordedVideoFile_Path_"></a>
<a id="E2E_RecordedVideoFile_Path"></a>

### Path


```csharp
public required string Path { get; init; }
```


Absolute path of the file.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L32)
