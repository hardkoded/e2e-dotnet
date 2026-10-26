---
title: "ProviderRecording"
description: "A recording a provider's service makes of the browser or device it leased, as RecordAsync started it."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRecording"
  hidden: true
---

<a id="E2E_Engine_ProviderRecording"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public abstract class ProviderRecording
```


A recording a provider's service makes of the browser or device it leased, as `RecordAsync` started it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L48)

## Properties

<a id="E2E_Engine_ProviderRecording_StartedAt_"></a>
<a id="E2E_Engine_ProviderRecording_StartedAt"></a>

### StartedAt


```csharp
public abstract DateTimeOffset StartedAt { get; }
```


When the recording started. Its first frame is at or just after it.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L51)

## Methods

<a id="E2E_Engine_ProviderRecording_StopAsync_"></a>
<a id="E2E_Engine_ProviderRecording_StopAsync_E2E_Engine_ProviderRecordingStopContext_System_Threading_CancellationToken_"></a>

### StopAsync(ProviderRecordingStopContext, CancellationToken)


```csharp
public abstract Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [ProviderRecordingStopContext](/api/E2E.Engine/ProviderRecordingStopContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ProviderRecordingResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L53)
