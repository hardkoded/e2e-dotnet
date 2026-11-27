---
title: "BrowserClock"
description: "Controls time in the pages of the attempt, as Playwright's page.clock does: Date, performance, timers, requestAnimationFrame, and requestIdleCallback in ever..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserClock"
  hidden: true
---

<a id="E2E_BrowserClock"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BrowserClock
```


Controls time in the pages of the attempt, as Playwright's `page.clock` does: `Date`, `performance`, timers,
`requestAnimationFrame`, and `requestIdleCallback` in every tab and frame, the ones the app opens included, from the call on.
A navigation keeps the clock; nothing outside the page (the test's own timeouts, the harness's waits) follows it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs)

## Methods

<a id="E2E_BrowserClock_FastForwardAsync_"></a>
<a id="E2E_BrowserClock_FastForwardAsync_E2E_Engine_ClockTicks_System_Threading_CancellationToken_"></a>

### FastForwardAsync(ClockTicks, CancellationToken)


```csharp
public Task FastForwardAsync(ClockTicks ticks, CancellationToken cancellationToken = default)
```


Jumps the clock ahead by &lt;code class="paramref"&gt;ticks&lt;/code&gt;, firing each due timer at most once, as when a laptop wakes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ticks` | [ClockTicks](/api/E2E.Engine/ClockTicks/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserClock_InstallAsync_"></a>
<a id="E2E_BrowserClock_InstallAsync_E2E_Engine_BrowserClockInstallOptions_System_Threading_CancellationToken_"></a>

### InstallAsync(BrowserClockInstallOptions?, CancellationToken)


```csharp
public Task InstallAsync(BrowserClockInstallOptions? options = null, CancellationToken cancellationToken = default)
```


Replaces the page's time functions with fake ones that start at [Time](/api/E2E.Engine/BrowserClockInstallOptions/#E2E_Engine_BrowserClockInstallOptions_Time) (default: now) and run on in real
time until `PauseAtAsync` or `SetFixedTimeAsync`. Every other method installs the clock first if needed, starting
at the Unix epoch, as Playwright's does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [BrowserClockInstallOptions](/api/E2E.Engine/BrowserClockInstallOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserClock_PauseAtAsync_"></a>
<a id="E2E_BrowserClock_PauseAtAsync_E2E_Engine_ClockTime_System_Threading_CancellationToken_"></a>

### PauseAtAsync(ClockTime, CancellationToken)


```csharp
public Task PauseAtAsync(ClockTime time, CancellationToken cancellationToken = default)
```


Fast-forwards to &lt;code class="paramref"&gt;time&lt;/code&gt; and pauses there, which stops the page's timers: they fire only through `RunForAsync` and `FastForwardAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `time` | [ClockTime](/api/E2E.Engine/ClockTime/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserClock_ResumeAsync_"></a>
<a id="E2E_BrowserClock_ResumeAsync_System_Threading_CancellationToken_"></a>

### ResumeAsync(CancellationToken)


```csharp
public Task ResumeAsync(CancellationToken cancellationToken = default)
```


Lets a paused clock run on in real time.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserClock_RunForAsync_"></a>
<a id="E2E_BrowserClock_RunForAsync_E2E_Engine_ClockTicks_System_Threading_CancellationToken_"></a>

### RunForAsync(ClockTicks, CancellationToken)


```csharp
public Task RunForAsync(ClockTicks ticks, CancellationToken cancellationToken = default)
```


Advances the clock by &lt;code class="paramref"&gt;ticks&lt;/code&gt;, firing every timer due on the way.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ticks` | [ClockTicks](/api/E2E.Engine/ClockTicks/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserClock_SetFixedTimeAsync_"></a>
<a id="E2E_BrowserClock_SetFixedTimeAsync_E2E_Engine_ClockTime_System_Threading_CancellationToken_"></a>

### SetFixedTimeAsync(ClockTime, CancellationToken)


```csharp
public Task SetFixedTimeAsync(ClockTime time, CancellationToken cancellationToken = default)
```


Makes `Date.now()` and `new Date()` return &lt;code class="paramref"&gt;time&lt;/code&gt; from now on. It fixes only `Date`: unlike `PauseAtAsync`, it does not pause timers, which keep running.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `time` | [ClockTime](/api/E2E.Engine/ClockTime/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserClock_SetSystemTimeAsync_"></a>
<a id="E2E_BrowserClock_SetSystemTimeAsync_E2E_Engine_ClockTime_System_Threading_CancellationToken_"></a>

### SetSystemTimeAsync(ClockTime, CancellationToken)


```csharp
public Task SetSystemTimeAsync(ClockTime time, CancellationToken cancellationToken = default)
```


Sets the wall time to &lt;code class="paramref"&gt;time&lt;/code&gt; without firing a timer, as when the system clock changes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `time` | [ClockTime](/api/E2E.Engine/ClockTime/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
