---
title: "ProviderRecordings"
description: "The recording seam every engine's provider shares (upstream recording.ts): a hosted browser or device service recording the attempt itself, in place of the e..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRecordings"
  hidden: true
---

<a id="E2E_Engine_ProviderRecordings"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class ProviderRecordings
```


The recording seam every engine's provider shares (upstream `recording.ts`): a hosted browser or device service
recording the attempt itself, in place of the engine's own capture. A provider's `RecordAsync` starts a
[ProviderRecording](/api/E2E.Engine/ProviderRecording/) for the lease the attempt rides; `CancellationToken)` ends it and
checks what the provider handed back, so every engine turns a recording into a [VideoSegment](/api/E2E.Engine/VideoSegment/) the same way.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L102)

## Methods

<a id="E2E_Engine_ProviderRecordings_StopProviderRecordingAsync_"></a>
<a id="E2E_Engine_ProviderRecordings_StopProviderRecordingAsync_E2E_Engine_ProviderRecording_E2E_Engine_ProviderRecordingTarget_System_Threading_CancellationToken_"></a>

### StopProviderRecordingAsync(ProviderRecording, ProviderRecordingTarget, CancellationToken)


```csharp
public static Task<VideoSegment> StopProviderRecordingAsync(ProviderRecording recording, ProviderRecordingTarget target, CancellationToken cancellationToken)
```


Ends a provider's recording and returns it as the attempt's segment: a file in the attempt's `video`
directory, or a link. What the provider handed back is checked, since the engine trusts nothing it did not write:
a file must be a plain name that exists in the directory, a link an `http(s)` URL with a media type. The URL
is kept in its parsed form and the start time as an ISO timestamp, the shapes the report schema admits. Every
failure names the provider and the lease.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `recording` | [ProviderRecording](/api/E2E.Engine/ProviderRecording/) |  |
| `target` | [ProviderRecordingTarget](/api/E2E.Engine/ProviderRecordingTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;VideoSegment&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L114)
