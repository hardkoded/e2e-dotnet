---
title: "IBrowserSession"
description: "The browser-shaped surface an engine session can add on top of IEngineSession. The Browser fixture uses it, and fails with UNSUPPORTED_CAPABILITY when the se..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IBrowserSession"
  hidden: true
---

<a id="E2E_Engine_IBrowserSession"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IBrowserSession : IEngineSession, IAsyncDisposable
```


The browser-shaped surface an engine session can add on top of
[IEngineSession](/api/E2E.Engine/IEngineSession/). The [Browser](/api/E2E/Browser/) fixture uses it, and
fails with `UNSUPPORTED_CAPABILITY` when the session does not implement it.
The harness has already validated every argument.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs)

## Methods

<a id="E2E_Engine_IBrowserSession_AddInitScriptAsync_"></a>
<a id="E2E_Engine_IBrowserSession_AddInitScriptAsync_System_String_System_Threading_CancellationToken_"></a>

### AddInitScriptAsync(string, CancellationToken)


```csharp
Task AddInitScriptAsync(string source, CancellationToken cancellationToken)
```


Adds page source every document runs before its own, from now on, in
this context and each one that replaces it. A session that does not
override it fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `source` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_ClearCookiesAsync_"></a>
<a id="E2E_Engine_IBrowserSession_ClearCookiesAsync_E2E_Engine_CookieFilter_System_Threading_CancellationToken_"></a>

### ClearCookiesAsync(CookieFilter?, CancellationToken)


```csharp
Task ClearCookiesAsync(CookieFilter? options, CancellationToken cancellationToken)
```


Removes the cookies matching every filter in &lt;code class="paramref"&gt;options&lt;/code&gt;; with none it removes every cookie. Other storage stays.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [CookieFilter](/api/E2E.Engine/CookieFilter/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_EmulateMediaAsync_"></a>
<a id="E2E_Engine_IBrowserSession_EmulateMediaAsync_E2E_Engine_WebMedia_System_Threading_CancellationToken_"></a>

### EmulateMediaAsync(WebMedia, CancellationToken)


```csharp
Task EmulateMediaAsync(WebMedia media, CancellationToken cancellationToken)
```


Emulates media features on every open tab and every tab opened after them, for the rest of the attempt;
a feature &lt;code class="paramref"&gt;media&lt;/code&gt; leaves out keeps its value. A session that does not override it fails
with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `media` | [WebMedia](/api/E2E.Engine/WebMedia/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_EvaluateAsync_"></a>
<a id="E2E_Engine_IBrowserSession_EvaluateAsync_System_String_System_Object_System_Boolean_System_Threading_CancellationToken_"></a>

### EvaluateAsync(string, object?, bool, CancellationToken)


```csharp
Task<JsonElement?> EvaluateAsync(string expression, object? arg, bool hasArg, CancellationToken cancellationToken)
```


Evaluates trusted test code in the page. &lt;code class="paramref"&gt;hasArg&lt;/code&gt; says whether &lt;code class="paramref"&gt;arg&lt;/code&gt; is passed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `arg` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
| `hasArg` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;JsonElement?&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_ForwardAsync_"></a>
<a id="E2E_Engine_IBrowserSession_ForwardAsync_System_Threading_CancellationToken_"></a>

### ForwardAsync(CancellationToken)


```csharp
Task ForwardAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_GetCookiesAsync_"></a>
<a id="E2E_Engine_IBrowserSession_GetCookiesAsync_System_Threading_CancellationToken_"></a>

### GetCookiesAsync(CancellationToken)


```csharp
Task<IReadOnlyList<BrowserCookie>> GetCookiesAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;BrowserCookie&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_GetTitleAsync_"></a>
<a id="E2E_Engine_IBrowserSession_GetTitleAsync_System_Threading_CancellationToken_"></a>

### GetTitleAsync(CancellationToken)


```csharp
Task<string> GetTitleAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_GetUrlAsync_"></a>
<a id="E2E_Engine_IBrowserSession_GetUrlAsync_System_Threading_CancellationToken_"></a>

### GetUrlAsync(CancellationToken)


```csharp
Task<string> GetUrlAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_GotoAsync_"></a>
<a id="E2E_Engine_IBrowserSession_GotoAsync_System_String_E2E_GotoWaitUntil_System_TimeSpan_System_Threading_CancellationToken_"></a>

### GotoAsync(string, GotoWaitUntil, TimeSpan, CancellationToken)


```csharp
Task GotoAsync(string url, GotoWaitUntil waitUntil, TimeSpan timeout, CancellationToken cancellationToken)
```


Navigates to &lt;code class="paramref"&gt;url&lt;/code&gt;, already resolved and checked, and waits
for &lt;code class="paramref"&gt;waitUntil&lt;/code&gt; within &lt;code class="paramref"&gt;timeout&lt;/code&gt;. A session
that does not override it fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `waitUntil` | [GotoWaitUntil](/api/E2E/GotoWaitUntil/) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_KeyboardDownAsync_"></a>
<a id="E2E_Engine_IBrowserSession_KeyboardDownAsync_System_String_System_Threading_CancellationToken_"></a>

### KeyboardDownAsync(string, CancellationToken)


```csharp
Task KeyboardDownAsync(string key, CancellationToken cancellationToken)
```


Holds one key as direct test input; a repeated down sends a repeat event.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_KeyboardPressAsync_"></a>
<a id="E2E_Engine_IBrowserSession_KeyboardPressAsync_System_String_System_Threading_CancellationToken_"></a>

### KeyboardPressAsync(string, CancellationToken)


```csharp
Task KeyboardPressAsync(string key, CancellationToken cancellationToken)
```


Sends one key as direct test input. Unlike `PressAsync`,
it never needs a fresh observation.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_KeyboardTypeAsync_"></a>
<a id="E2E_Engine_IBrowserSession_KeyboardTypeAsync_System_String_System_Threading_CancellationToken_"></a>

### KeyboardTypeAsync(string, CancellationToken)


```csharp
Task KeyboardTypeAsync(string text, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_KeyboardUpAsync_"></a>
<a id="E2E_Engine_IBrowserSession_KeyboardUpAsync_System_String_System_Threading_CancellationToken_"></a>

### KeyboardUpAsync(string, CancellationToken)


```csharp
Task KeyboardUpAsync(string key, CancellationToken cancellationToken)
```


Releases a key held by `KeyboardDownAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_MouseDownAsync_"></a>
<a id="E2E_Engine_IBrowserSession_MouseDownAsync_System_Threading_CancellationToken_"></a>

### MouseDownAsync(CancellationToken)


```csharp
Task MouseDownAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_MouseMoveAsync_"></a>
<a id="E2E_Engine_IBrowserSession_MouseMoveAsync_System_Single_System_Single_System_Threading_CancellationToken_"></a>

### MouseMoveAsync(float, float, CancellationToken)


```csharp
Task MouseMoveAsync(float x, float y, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `x` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `y` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_MouseUpAsync_"></a>
<a id="E2E_Engine_IBrowserSession_MouseUpAsync_System_Threading_CancellationToken_"></a>

### MouseUpAsync(CancellationToken)


```csharp
Task MouseUpAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_MouseWheelAsync_"></a>
<a id="E2E_Engine_IBrowserSession_MouseWheelAsync_System_Single_System_Single_System_Threading_CancellationToken_"></a>

### MouseWheelAsync(float, float, CancellationToken)


```csharp
Task MouseWheelAsync(float deltaX, float deltaY, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `deltaX` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `deltaY` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_OnDialogAsync_"></a>
<a id="E2E_Engine_IBrowserSession_OnDialogAsync_E2E_Engine_DialogHandler_System_Threading_CancellationToken_"></a>

### OnDialogAsync(DialogHandler, CancellationToken)


```csharp
Task<Action> OnDialogAsync(DialogHandler handler, CancellationToken cancellationToken)
```


Registers a handler for native dialogs (the newest wins) and returns its unsubscribe, which is idempotent. A dialog with no
handler fails the next step, and is dismissed. A session that does not override it fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `handler` | [DialogHandler](/api/E2E.Engine/DialogHandler/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Action&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_ReloadAsync_"></a>
<a id="E2E_Engine_IBrowserSession_ReloadAsync_System_Threading_CancellationToken_"></a>

### ReloadAsync(CancellationToken)


```csharp
Task ReloadAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_RouteAsync_"></a>
<a id="E2E_Engine_IBrowserSession_RouteAsync_System_Func_System_String_System_Boolean__System_Func_E2E_Engine_IBrowserRoute_System_Threading_Tasks_Task__System_Threading_CancellationToken_"></a>

### `RouteAsync(Func<string, bool>, Func<IBrowserRoute, Task>, CancellationToken)`


```csharp
Task RouteAsync(Func<string, bool> matches, Func<IBrowserRoute, Task> handler, CancellationToken cancellationToken)
```


Adds a network route for the rest of the attempt, every context it opens
included. The route registered last runs first. When &lt;code class="paramref"&gt;handler&lt;/code&gt;
throws, the session keeps the error, fails its next operation with it, and
aborts the request if the handler took no decision. A session that does not
override it fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `matches` | [Func&lt;string, bool&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `handler` | [Func&lt;IBrowserRoute, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_SetCookiesAsync_"></a>
<a id="E2E_Engine_IBrowserSession_SetCookiesAsync_System_Collections_Generic_IReadOnlyList_E2E_Engine_BrowserCookie__System_Threading_CancellationToken_"></a>

### `SetCookiesAsync(IReadOnlyList<BrowserCookie>, CancellationToken)`


```csharp
Task SetCookiesAsync(IReadOnlyList<BrowserCookie> cookies, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cookies` | [IReadOnlyList&lt;BrowserCookie&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_SetViewportAsync_"></a>
<a id="E2E_Engine_IBrowserSession_SetViewportAsync_System_Int32_System_Int32_System_Threading_CancellationToken_"></a>

### SetViewportAsync(int, int, CancellationToken)


```csharp
Task SetViewportAsync(int width, int height, CancellationToken cancellationToken)
```


Sizes the open page, and every page the session opens later in the attempt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_UnrouteAsync_"></a>
<a id="E2E_Engine_IBrowserSession_UnrouteAsync_System_Func_E2E_Engine_IBrowserRoute_System_Threading_Tasks_Task__System_Threading_CancellationToken_"></a>

### `UnrouteAsync(Func<IBrowserRoute, Task>, CancellationToken)`


```csharp
Task UnrouteAsync(Func<IBrowserRoute, Task> handler, CancellationToken cancellationToken)
```


Removes the route `RouteAsync` added with &lt;code class="paramref"&gt;handler&lt;/code&gt;. A session that does not override it fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `handler` | [Func&lt;IBrowserRoute, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserSession_WaitForDownloadAsync_"></a>
<a id="E2E_Engine_IBrowserSession_WaitForDownloadAsync_System_Func_System_Threading_Tasks_Task__System_TimeSpan_System_Threading_CancellationToken_"></a>

### `WaitForDownloadAsync(Func<Task>, TimeSpan, CancellationToken)`


```csharp
Task<BrowserDownload> WaitForDownloadAsync(Func<Task> trigger, TimeSpan timeout, CancellationToken cancellationToken)
```


Runs &lt;code class="paramref"&gt;trigger&lt;/code&gt; and waits up to &lt;code class="paramref"&gt;timeout&lt;/code&gt; for the download it starts, saved under the attempt's
`downloads` artifact folder. A trigger that throws fails the call with its own error. A session that does not override it
fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `trigger` | [Func&lt;Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BrowserDownload&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_WaitForDownloadsAsync_"></a>
<a id="E2E_Engine_IBrowserSession_WaitForDownloadsAsync_System_Func_System_Threading_Tasks_Task__System_Int32_System_TimeSpan_System_Threading_CancellationToken_"></a>

### `WaitForDownloadsAsync(Func<Task>, int, TimeSpan, CancellationToken)`


```csharp
Task<IReadOnlyList<BrowserDownload>> WaitForDownloadsAsync(Func<Task> trigger, int count, TimeSpan timeout, CancellationToken cancellationToken)
```


Runs &lt;code class="paramref"&gt;trigger&lt;/code&gt; once and waits up to &lt;code class="paramref"&gt;timeout&lt;/code&gt; for the first &lt;code class="paramref"&gt;count&lt;/code&gt; downloads it
starts, saved under the attempt's `downloads` artifact folder in start order. One budget bounds the start, completion and
save of every file. A trigger that throws fails the call with its own error. A session that does not override it fails with
`UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `trigger` | [Func&lt;Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `count` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;BrowserDownload&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserSession_WaitForResponseAsync_"></a>
<a id="E2E_Engine_IBrowserSession_WaitForResponseAsync_System_Func_System_String_System_Boolean__System_TimeSpan_System_Threading_CancellationToken_"></a>

### `WaitForResponseAsync(Func<string, bool>, TimeSpan, CancellationToken)`


```csharp
Task<BrowserResponse> WaitForResponseAsync(Func<string, bool> matches, TimeSpan timeout, CancellationToken cancellationToken)
```


Waits up to &lt;code class="paramref"&gt;timeout&lt;/code&gt; for a response whose URL &lt;code class="paramref"&gt;matches&lt;/code&gt;
accepts. The timeout bounds the match only: the body read starts once the
response is known and goes on in [Body](/api/E2E.Engine/BrowserResponse/#E2E_Engine_BrowserResponse_Body). A session
that does not override it fails with `UNSUPPORTED_CAPABILITY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `matches` | [Func&lt;string, bool&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BrowserResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
