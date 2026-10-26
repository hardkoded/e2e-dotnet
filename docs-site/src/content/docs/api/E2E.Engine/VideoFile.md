---
title: "VideoFile"
description: "A video file in the attempt artifact directory."
editUrl: false
prev: false
next: false
sidebar:
  label: "VideoFile"
  hidden: true
---

<a id="E2E_Engine_VideoFile"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record VideoFile : VideoSegment, IEquatable<VideoSegment>, IEquatable<VideoFile>
```


A video file in the attempt artifact directory.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [VideoSegment](/api/E2E.Engine/VideoSegment/)

**Implements:** [IEquatable&lt;VideoSegment&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;VideoFile&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L80)

## Properties

<a id="E2E_Engine_VideoFile_Path_"></a>
<a id="E2E_Engine_VideoFile_Path"></a>

### Path


```csharp
public required string Path { get; init; }
```


Path relative to the attempt artifact directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L83)
