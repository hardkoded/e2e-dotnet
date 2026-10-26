---
title: "VideoSegment"
description: "One recorded video of an attempt: a file the engine wrote into the attempt artifact directory (VideoFile), or a recording a hosted service keeps and links to..."
editUrl: false
prev: false
next: false
sidebar:
  label: "VideoSegment"
  hidden: true
---

<a id="E2E_Engine_VideoSegment"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public abstract record VideoSegment : IEquatable<VideoSegment>
```


One recorded video of an attempt: a file the engine wrote into the attempt artifact directory
([VideoFile](/api/E2E.Engine/VideoFile/)), or a recording a hosted service keeps and links to ([VideoLink](/api/E2E.Engine/VideoLink/)).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;VideoSegment&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [VideoFile](/api/E2E.Engine/VideoFile/), [VideoLink](/api/E2E.Engine/VideoLink/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L73)

## Properties

<a id="E2E_Engine_VideoSegment_StartedAt_"></a>
<a id="E2E_Engine_VideoSegment_StartedAt"></a>

### StartedAt


```csharp
public required string StartedAt { get; init; }
```


When the segment started recording, as an ISO timestamp; its first frame is at or just after it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L76)
