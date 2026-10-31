---
title: "RecordedVideoLink"
description: "A recording a hosted browser or device service keeps, by URL (upstream's RecordedVideoLink)."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedVideoLink"
  hidden: true
---

<a id="E2E_RecordedVideoLink"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record RecordedVideoLink : RecordedVideo, IEquatable<RecordedVideo>, IEquatable<RecordedVideoLink>
```


A recording a hosted browser or device service keeps, by URL (upstream's `RecordedVideoLink`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RecordedVideo](/api/E2E/RecordedVideo/)

**Implements:** [IEquatable&lt;RecordedVideo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;RecordedVideoLink&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L36)

## Properties

<a id="E2E_RecordedVideoLink_MediaType_"></a>
<a id="E2E_RecordedVideoLink_MediaType"></a>

### MediaType


```csharp
public required string MediaType { get; init; }
```


Media type of what the URL serves: `video/mp4`, or `text/html` for a player page.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L42)

<a id="E2E_RecordedVideoLink_Url_"></a>
<a id="E2E_RecordedVideoLink_Url"></a>

### Url


```csharp
public required string Url { get; init; }
```


`http(s)` URL of the recording.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L39)
