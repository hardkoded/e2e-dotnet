---
title: "Session"
description: "One isolated session: a browser context, or the app on a device. Holds Screen, App, Platform, and Browser. Upstream's Session and SessionControls (core/launc..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Session"
  hidden: true
---

<a id="E2E_Session"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Session : IAsyncDisposable
```


One isolated session: a browser context, or the app on a device. Holds [Screen](#E2E_Session_Screen), [App](#E2E_Session_App), [Platform](#E2E_Session_Platform), and
[Browser](#E2E_Session_Browser). Upstream's `Session` and `SessionControls` (`core/launch.ts`), over the port's [E2ESession](/api/E2E/E2ESession/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Properties

<a id="E2E_Session_App_"></a>
<a id="E2E_Session_App"></a>

### App


```csharp
public App App { get; }
```


App lifecycle: open, restart, clear state, back, screenshot, recording.

**Value:** [App](/api/E2E/App/)

<a id="E2E_Session_Browser_"></a>
<a id="E2E_Session_Browser"></a>

### Browser


```csharp
public Browser Browser { get; }
```


The browser fixture. Its members fail with `UNSUPPORTED_CAPABILITY` on an engine without a browser.

**Value:** [Browser](/api/E2E/Browser/)

<a id="E2E_Session_Platform_"></a>
<a id="E2E_Session_Platform"></a>

### Platform


```csharp
public string Platform { get; }
```


The engine's platform label: `web`, `ios`, `android`, or an engine's own string.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Session_Screen_"></a>
<a id="E2E_Session_Screen"></a>

### Screen


```csharp
public Screen Screen { get; }
```


Semantic queries and the actions on their matches.

**Value:** [Screen](/api/E2E/Screen/)

## Methods

<a id="E2E_Session_CloseAsync_"></a>
<a id="E2E_Session_CloseAsync"></a>

### CloseAsync()


```csharp
public Task CloseAsync()
```


Ends the session. Idempotent. Throws an [AggregateException](https://learn.microsoft.com/dotnet/api/system.aggregateexception) of the failures closing found, such as a cleanup that did not finish. Upstream's `close`.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Session_DisposeAsync_"></a>
<a id="E2E_Session_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Ends the session. Idempotent. Throws an [AggregateException](https://learn.microsoft.com/dotnet/api/system.aggregateexception) of the failures closing found, such as a cleanup that did not finish. Upstream's `close`.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

<a id="E2E_Session_SaveStateAsync_"></a>
<a id="E2E_Session_SaveStateAsync_System_Threading_CancellationToken_"></a>

### SaveStateAsync(CancellationToken)


```csharp
public Task<SessionState> SaveStateAsync(CancellationToken cancellationToken = default)
```


Captures the session's state (cookies, storage, app data) in memory. Persist it only with [ExportAsync(SessionStateKeyOptions)](/api/E2E/SessionState/#E2E_SessionState_ExportAsync_E2E_SessionStateKeyOptions_). Upstream's `saveState`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;SessionState&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Session_SetDefaultTimeout_"></a>
<a id="E2E_Session_SetDefaultTimeout_System_TimeSpan_"></a>

### SetDefaultTimeout(TimeSpan)


```csharp
public void SetDefaultTimeout(TimeSpan timeout)
```


The default timeout of operations started from now on: actions, waits, reads, and app opens and restarts. A running operation keeps the
deadline it started with. Throws `INVALID_ARGUMENT` unless &lt;code class="paramref"&gt;timeout&lt;/code&gt; is positive and finite. Upstream's `setDefaultTimeout`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
