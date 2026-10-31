---
title: "IEngineSession"
description: "One attempt's session with the app. Disposed at the end of the attempt, after afterEach."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngineSession"
  hidden: true
---

<a id="E2E_Engine_IEngineSession"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngineSession : IAsyncDisposable
```


One attempt's session with the app. Disposed at the end of the attempt, after `afterEach`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L170)

## Methods

<a id="E2E_Engine_IEngineSession_CaptureStateAsync_"></a>
<a id="E2E_Engine_IEngineSession_CaptureStateAsync_System_Threading_CancellationToken_"></a>

### CaptureStateAsync(CancellationToken)


```csharp
Task<EngineState> CaptureStateAsync(CancellationToken cancellationToken)
```


Captures the app's state (cookies and storage) as a payload another session can restore. The payload's `Data` is a mutable
`JsonNode`, so a caller must not change it between capture and restore. Upstream's optional
`captureState`; a session that keeps none fails with `UNSUPPORTED_CAPABILITY`, as upstream's `session.save()` does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)&lt;[EngineState](/api/E2E/EngineState/)&gt;

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L264)

<a id="E2E_Engine_IEngineSession_RestoreStateAsync_"></a>
<a id="E2E_Engine_IEngineSession_RestoreStateAsync_E2E_EngineState_System_Threading_CancellationToken_"></a>

### RestoreStateAsync(EngineState, CancellationToken)


```csharp
Task RestoreStateAsync(EngineState state, CancellationToken cancellationToken)
```


Replaces the app's current state with one [CaptureStateAsync(CancellationToken)](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_CaptureStateAsync_System_Threading_CancellationToken_) captured. Upstream's optional `restoreState`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `state` | [EngineState](/api/E2E/EngineState/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L271)
