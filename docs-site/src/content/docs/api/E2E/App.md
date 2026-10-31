---
title: "App"
description: "Opens and resets the app under test. A relative URL is resolved against BaseUrl; no URL opens the base URL."
editUrl: false
prev: false
next: false
sidebar:
  label: "App"
  hidden: true
---

<a id="E2E_App"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class App
```


Opens and resets the app under test. A relative URL is resolved against
[BaseUrl](#E2E_App_BaseUrl); no URL opens the base URL.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L41)

## Properties

<a id="E2E_App_BaseUrl_"></a>
<a id="E2E_App_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; }
```


The resolved base URL of the target's app, with the port the run allocated when `app.url` declared port 0 and no trailing
slash, so `$"{BaseUrl}/api/users"` joins with one slash; null when the test declares none. [OpenAsync(string?, CancellationToken)](#E2E_App_OpenAsync_System_String_System_Threading_CancellationToken_) still
resolves a relative URL against the base path.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L66)

## Methods

<a id="E2E_App_BackAsync_"></a>
<a id="E2E_App_BackAsync_System_Threading_CancellationToken_"></a>

### BackAsync(CancellationToken)


```csharp
public Task BackAsync(CancellationToken cancellationToken = default)
```


Goes back one entry in the history. With no earlier entry it does nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L104)

<a id="E2E_App_ClearStateAsync_"></a>
<a id="E2E_App_ClearStateAsync_System_Threading_CancellationToken_"></a>

### ClearStateAsync(CancellationToken)


```csharp
public Task ClearStateAsync(CancellationToken cancellationToken = default)
```


Discards cookies, storage, and history, and reopens the app at [BaseUrl](#E2E_App_BaseUrl).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L116)

<a id="E2E_App_OpenAsync_"></a>
<a id="E2E_App_OpenAsync_System_String_System_Threading_CancellationToken_"></a>

### OpenAsync(string?, CancellationToken)


```csharp
public Task OpenAsync(string? url = null, CancellationToken cancellationToken = default)
```


Opens a path or URL, resolved against the base URL. No URL opens the base URL itself.
Throws `APP_URL_REQUIRED` for a relative URL when the test declares no base URL, and `POLICY_DENIED`
for a malformed URL or a scheme other than `http:` or `https:` (the exact `about:blank` is admitted).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L73)

<a id="E2E_App_RestartAsync_"></a>
<a id="E2E_App_RestartAsync_System_Threading_CancellationToken_"></a>

### RestartAsync(CancellationToken)


```csharp
public Task RestartAsync(CancellationToken cancellationToken = default)
```


Closes the current document and reopens the app at [BaseUrl](#E2E_App_BaseUrl). Cookies and storage are kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L110)

<a id="E2E_App_ScreenshotAsync_"></a>
<a id="E2E_App_ScreenshotAsync_System_String_System_Threading_CancellationToken_"></a>

### ScreenshotAsync(string?, CancellationToken)


```csharp
public Task<string> ScreenshotAsync(string? label = null, CancellationToken cancellationToken = default)
```


Captures a redacted evidence screenshot: secure fields are masked at the source. The file is an attempt artifact under
`screenshots/`, and the returned path is absolute (upstream's `app.screenshot`, `createApp` in `run/fixtures.ts`).
Throws `UNSUPPORTED_CAPABILITY` on an engine that keeps no screenshots.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Names the step and joins the file name. |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) | Cancels the capture. |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L128)

<a id="E2E_App_StartRecordingAsync_"></a>
<a id="E2E_App_StartRecordingAsync_E2E_RecordingOptions_System_Threading_CancellationToken_"></a>

### StartRecordingAsync(RecordingOptions?, CancellationToken)


```csharp
public Task StartRecordingAsync(RecordingOptions? options = null, CancellationToken cancellationToken = default)
```


Starts recording the screen, until [StopRecordingAsync(CancellationToken)](#E2E_App_StopRecordingAsync_System_Threading_CancellationToken_). One recording at a time, and none while the test's
`video` records, since the engine has one recorder: set the test's video to `off`. A recording still running when the
test ends is stopped and kept. Like the test's video, a recording masks nothing. Upstream's `app.startRecording`.
Throws `UNSUPPORTED_CAPABILITY` while the test's video records or when the engine records no video, and
`INVALID_ARGUMENT` when another recording is running.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [RecordingOptions](/api/E2E/RecordingOptions/) | Names the file: `recordings/001-<name>.<ext>`. |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) | Cancels the start. |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L151)

<a id="E2E_App_StopRecordingAsync_"></a>
<a id="E2E_App_StopRecordingAsync_System_Threading_CancellationToken_"></a>

### StopRecordingAsync(CancellationToken)


```csharp
public Task<IReadOnlyList<RecordedVideo>> StopRecordingAsync(CancellationToken cancellationToken = default)
```


Stops the recording and returns its videos, in play order: a file (absolute `Path`) under the attempt's `recordings/`,
named `001-<name>.<ext>`, or a link when a hosted browser or device keeps the recording. There is one video, except on
the web when the page was replaced while it recorded (`app.RestartAsync()`, `app.ClearStateAsync()`): then one per page.
The step links each video in the trace. Never empty. Upstream's `app.stopRecording`. Throws `INVALID_ARGUMENT` when no
recording is running and `ENGINE_FAILURE` when the engine wrote no video.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) | Cancels the stop. |

**Returns:** [Task&lt;IReadOnlyList&lt;RecordedVideo&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L166)
