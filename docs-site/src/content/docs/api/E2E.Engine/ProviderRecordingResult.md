---
title: "ProviderRecordingResult"
description: "What a stopped recording became: a file the provider wrote into the directory it was given, by name (File), or a recording its service keeps, by http(s) URL..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRecordingResult"
  hidden: true
---

<a id="E2E_Engine_ProviderRecordingResult"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ProviderRecordingResult : IEquatable<ProviderRecordingResult>
```


What a stopped recording became: a file the provider wrote into the directory it was given, by name
([File](#E2E_Engine_ProviderRecordingResult_File)), or a recording its service keeps, by `http(s)` URL and media type
([Url](#E2E_Engine_ProviderRecordingResult_Url) and [MediaType](#E2E_Engine_ProviderRecordingResult_MediaType)).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ProviderRecordingResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L35)

## Properties

<a id="E2E_Engine_ProviderRecordingResult_File_"></a>
<a id="E2E_Engine_ProviderRecordingResult_File"></a>

### File


```csharp
public string? File { get; init; }
```


The plain name of a file written into [Dir](/api/E2E.Engine/ProviderRecordingStopContext/#E2E_Engine_ProviderRecordingStopContext_Dir).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L38)

<a id="E2E_Engine_ProviderRecordingResult_MediaType_"></a>
<a id="E2E_Engine_ProviderRecordingResult_MediaType"></a>

### MediaType


```csharp
public string? MediaType { get; init; }
```


The media type [Url](#E2E_Engine_ProviderRecordingResult_Url) serves: `video/mp4`, or `text/html` for a player page.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L44)

<a id="E2E_Engine_ProviderRecordingResult_Url_"></a>
<a id="E2E_Engine_ProviderRecordingResult_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


The `http(s)` URL of a recording the service keeps.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L41)
