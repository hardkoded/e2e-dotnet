---
title: "VideoLink"
description: "A recording a hosted service keeps, by URL: the report links to it and the runner never downloads it."
editUrl: false
prev: false
next: false
sidebar:
  label: "VideoLink"
  hidden: true
---

<a id="E2E_Engine_VideoLink"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record VideoLink : VideoSegment, IEquatable<VideoSegment>, IEquatable<VideoLink>
```


A recording a hosted service keeps, by URL: the report links to it and the runner never downloads it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [VideoSegment](/api/E2E.Engine/VideoSegment/)

**Implements:** [IEquatable&lt;VideoSegment&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;VideoLink&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L87)

## Properties

<a id="E2E_Engine_VideoLink_MediaType_"></a>
<a id="E2E_Engine_VideoLink_MediaType"></a>

### MediaType


```csharp
public required string MediaType { get; init; }
```


Media type of what the URL serves: `video/mp4`, or `text/html` for a player page.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L93)

<a id="E2E_Engine_VideoLink_Url_"></a>
<a id="E2E_Engine_VideoLink_Url"></a>

### Url


```csharp
public required string Url { get; init; }
```


`http(s)` URL of the recording.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L90)
