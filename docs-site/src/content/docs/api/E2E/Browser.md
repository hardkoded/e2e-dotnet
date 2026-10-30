---
title: "Browser"
description: "The browser the attempt drives, showing one active tab. It needs an engine session that implements IBrowserSession, such as WebEngine; on any other engine ea..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Browser"
  hidden: true
---

<a id="E2E_Browser"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Browser
```


The browser the attempt drives, showing one active tab. It needs an engine
session that implements [IBrowserSession](/api/E2E.Engine/IBrowserSession/), such as
[WebEngine](/api/E2E.Engine/WebEngine/); on any other engine each member fails with
`UNSUPPORTED_CAPABILITY`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L18)

## Properties

<a id="E2E_Browser_Keyboard_"></a>
<a id="E2E_Browser_Keyboard"></a>

### Keyboard


```csharp
public BrowserKeyboard Keyboard { get; }
```


Viewport-level keyboard, for whatever has focus.

**Value:** [BrowserKeyboard](/api/E2E/BrowserKeyboard/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L53)

<a id="E2E_Browser_Mouse_"></a>
<a id="E2E_Browser_Mouse"></a>

### Mouse


```csharp
public BrowserMouse Mouse { get; }
```


Direct pointer input from test code, independent of agent observations.

**Value:** [BrowserMouse](/api/E2E/BrowserMouse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L56)

## Methods

<a id="E2E_Browser_AddInitScriptAsync_"></a>
<a id="E2E_Browser_AddInitScriptAsync_E2E_Engine_WebInitScript_System_Object_System_Threading_CancellationToken_"></a>

### AddInitScriptAsync(WebInitScript, object?, CancellationToken)


```csharp
public Task AddInitScriptAsync(WebInitScript script, object? arg, CancellationToken cancellationToken = default)
```


Adds an init script function ([FromFunction(string)](/api/E2E.Engine/WebInitScript/#E2E_Engine_WebInitScript_FromFunction_System_String_)) the
page calls with one JSON-safe argument. Only a function takes an argument.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `script` | [WebInitScript](/api/E2E.Engine/WebInitScript/) |  |
| `arg` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L282)

<a id="E2E_Browser_AddInitScriptAsync_E2E_Engine_WebInitScript_System_Threading_CancellationToken_"></a>

### AddInitScriptAsync(WebInitScript, CancellationToken)


```csharp
public Task AddInitScriptAsync(WebInitScript script, CancellationToken cancellationToken = default)
```


Adds an init script: source, a file of it ([FromPath(string)](/api/E2E.Engine/WebInitScript/#E2E_Engine_WebInitScript_FromPath_System_String_)),
or a function the page calls with no argument ([FromFunction(string)](/api/E2E.Engine/WebInitScript/#E2E_Engine_WebInitScript_FromFunction_System_String_)).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `script` | [WebInitScript](/api/E2E.Engine/WebInitScript/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L275)

<a id="E2E_Browser_AddInitScriptAsync_System_String_System_Threading_CancellationToken_"></a>

### AddInitScriptAsync(string, CancellationToken)


```csharp
public Task AddInitScriptAsync(string script, CancellationToken cancellationToken = default)
```


Adds JavaScript source every document runs before the page's own, in
every tab and frame, from the next navigation on, for the rest of the attempt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `script` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L268)

<a id="E2E_Browser_BackAsync_"></a>
<a id="E2E_Browser_BackAsync_System_Threading_CancellationToken_"></a>

### BackAsync(CancellationToken)


```csharp
public Task BackAsync(CancellationToken cancellationToken = default)
```


Navigates the history back once.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L112)

<a id="E2E_Browser_ClearCookiesAsync_"></a>
<a id="E2E_Browser_ClearCookiesAsync_E2E_Engine_CookieFilter_System_Threading_CancellationToken_"></a>

### ClearCookiesAsync(CookieFilter?, CancellationToken)


```csharp
public Task ClearCookiesAsync(CookieFilter? options = null, CancellationToken cancellationToken = default)
```


Removes the cookies matching every filter in &lt;code class="paramref"&gt;options&lt;/code&gt;. A string matches a field exactly and a regular expression
matches its text (Playwright hands the source to the browser, which runs it as JavaScript). With no options it removes every cookie, HttpOnly ones included. Local and session storage stay.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [CookieFilter](/api/E2E.Engine/CookieFilter/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L304)

<a id="E2E_Browser_CookiesAsync_"></a>
<a id="E2E_Browser_CookiesAsync_System_Threading_CancellationToken_"></a>

### CookiesAsync(CancellationToken)


```csharp
public Task<IReadOnlyList<BrowserCookie>> CookiesAsync(CancellationToken cancellationToken = default)
```


Returns the cookies visible to the browser context.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;BrowserCookie&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L286)

<a id="E2E_Browser_EmulateMediaAsync_"></a>
<a id="E2E_Browser_EmulateMediaAsync_E2E_Engine_WebMedia_System_Threading_CancellationToken_"></a>

### EmulateMediaAsync(WebMedia, CancellationToken)


```csharp
public Task EmulateMediaAsync(WebMedia media, CancellationToken cancellationToken = default)
```


Emulates CSS media features on every open tab and every tab opened after them, the app's own included,
for the rest of the attempt, through `App.RestartAsync` and `App.ClearStateAsync`: the page's
`@media` rules and `matchMedia` both read them. A feature left out keeps its value. It works
before the first navigation, so the page loads with them.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `media` | [WebMedia](/api/E2E.Engine/WebMedia/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L349)

<a id="E2E_Browser_EvaluateAsync_"></a>
<a id="E2E_Browser_EvaluateAsync__1_System_String_System_Object_System_Threading_CancellationToken_"></a>

### `EvaluateAsync<T>(string, object?, CancellationToken)`


```csharp
public Task<T?> EvaluateAsync<T>(string expression, object? arg, CancellationToken cancellationToken = default)
```


Evaluates a function source in the page with one JSON-safe argument.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `arg` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L203)

<a id="E2E_Browser_EvaluateAsync__1_System_String_System_Threading_CancellationToken_"></a>

### `EvaluateAsync<T>(string, CancellationToken)`


```csharp
public Task<T?> EvaluateAsync<T>(string expression, CancellationToken cancellationToken = default)
```


Evaluates trusted test code in the page: an expression, or a function
source that is called. The result must be JSON-safe and is deserialized to
&lt;code class="typeparamref"&gt;T&lt;/code&gt;. A throwing script fails with `EVALUATE_FAILED`, and a
script that does not finish within the action timeout with `OPERATION_TIMEOUT`.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L199)

<a id="E2E_Browser_ForwardAsync_"></a>
<a id="E2E_Browser_ForwardAsync_System_Threading_CancellationToken_"></a>

### ForwardAsync(CancellationToken)


```csharp
public Task ForwardAsync(CancellationToken cancellationToken = default)
```


Navigates the history forward once.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L116)

<a id="E2E_Browser_FrameLocator_"></a>
<a id="E2E_Browser_FrameLocator_System_String_"></a>

### FrameLocator(string)


```csharp
public FrameScreen FrameLocator(string selector)
```


Creates a screen query scope inside one iframe: every [Screen](/api/E2E/Screen/) query answers from inside the frame. The frame is the
one element &lt;code class="paramref"&gt;selector&lt;/code&gt; names; none yet is polled for by an action, and answers no match to a read. Upstream's
`browser.frameLocator`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `selector` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [FrameScreen](/api/E2E/FrameScreen/)

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `INVALID_LOCATOR`: &lt;code class="paramref"&gt;selector&lt;/code&gt; is empty. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L180)

<a id="E2E_Browser_GotoAsync_"></a>
<a id="E2E_Browser_GotoAsync_System_String_E2E_GotoOptions_System_Threading_CancellationToken_"></a>

### GotoAsync(string, GotoOptions?, CancellationToken)


```csharp
public Task GotoAsync(string url, GotoOptions? options = null, CancellationToken cancellationToken = default)
```


Navigates to &lt;code class="paramref"&gt;url&lt;/code&gt;, resolved against the base URL like `App.OpenAsync`,
and waits for [WaitUntil](/api/E2E/GotoOptions/#E2E_GotoOptions_WaitUntil) within [Timeout](/api/E2E/GotoOptions/#E2E_GotoOptions_Timeout).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [GotoOptions](/api/E2E/GotoOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L92)

<a id="E2E_Browser_Locator_"></a>
<a id="E2E_Browser_Locator_System_String_"></a>

### Locator(string)


```csharp
public Locator Locator(string selector)
```


Creates a web-only locator from a CSS or XPath selector, for a control with no accessible name. Same polling and strictness as any
locator. A selector that captures with `*` before its last part (`*css=article >> text=Hello`) is refused with
`INVALID_LOCATOR` when the locator resolves: narrow with [Filter(Locator)](/api/E2E/Locator/#E2E_Locator_Filter_E2E_Locator_) instead. Upstream's `browser.locator`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `selector` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Locator](/api/E2E/Locator/)

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `INVALID_LOCATOR`: &lt;code class="paramref"&gt;selector&lt;/code&gt; is empty. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L167)

<a id="E2E_Browser_OnDialogAsync_"></a>
<a id="E2E_Browser_OnDialogAsync_E2E_Engine_DialogHandler_System_Threading_CancellationToken_"></a>

### OnDialogAsync(DialogHandler, CancellationToken)


```csharp
public Task<Func<Task>> OnDialogAsync(DialogHandler handler, CancellationToken cancellationToken = default)
```


Registers a handler for native dialogs, of every tab, for the rest of the attempt, and returns its unsubscribe. The newest
registration answers. A dialog with no handler fails the next step and is dismissed; a handler that returns without calling
[AcceptAsync(string)](/api/E2E.Engine/Dialog/#E2E_Engine_Dialog_AcceptAsync_System_String_) or [DismissAsync()](/api/E2E.Engine/Dialog/#E2E_Engine_Dialog_DismissAsync), or throws, fails the next step with that error, and its
dialog is dismissed. Pass [Accept](/api/E2E.Engine/DialogHandler/#E2E_Engine_DialogHandler_Accept) or [Dismiss](/api/E2E.Engine/DialogHandler/#E2E_Engine_DialogHandler_Dismiss) to answer them all alike.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `handler` | [DialogHandler](/api/E2E.Engine/DialogHandler/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Func&lt;Task&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L369)

<a id="E2E_Browser_OnDialogAsync_System_Func_E2E_Engine_Dialog_System_Threading_Tasks_Task__System_Threading_CancellationToken_"></a>

### `OnDialogAsync(Func<Dialog, Task>, CancellationToken)`


```csharp
public Task<Func<Task>> OnDialogAsync(Func<Dialog, Task> handler, CancellationToken cancellationToken = default)
```


Registers a handler that runs &lt;code class="paramref"&gt;handler&lt;/code&gt; for each dialog. See `CancellationToken)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `handler` | [Func&lt;Dialog, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Func&lt;Task&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L383)

<a id="E2E_Browser_ReloadAsync_"></a>
<a id="E2E_Browser_ReloadAsync_System_Threading_CancellationToken_"></a>

### ReloadAsync(CancellationToken)


```csharp
public Task ReloadAsync(CancellationToken cancellationToken = default)
```


Reloads the current document.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L108)

<a id="E2E_Browser_RouteAsync_"></a>
<a id="E2E_Browser_RouteAsync_System_String_System_Func_E2E_WebRoute_System_Threading_Tasks_Task__System_Threading_CancellationToken_"></a>

### `RouteAsync(string, Func<WebRoute, Task>, CancellationToken)`


```csharp
public Task RouteAsync(string pattern, Func<WebRoute, Task> handler, CancellationToken cancellationToken = default)
```


Adds a network route for the rest of the attempt, matching a complete URL with
the glob &lt;code class="paramref"&gt;pattern&lt;/code&gt; that `CancellationToken)`
takes. The route registered last runs first. &lt;code class="paramref"&gt;handler&lt;/code&gt; must take
exactly one decision on the [WebRoute](/api/E2E/WebRoute/). A handler that takes none,
takes two, or throws fails the next step with that error, and an undecided
request is aborted.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `handler` | [Func&lt;WebRoute, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L233)

<a id="E2E_Browser_RouteAsync_System_Text_RegularExpressions_Regex_System_Func_E2E_WebRoute_System_Threading_Tasks_Task__System_Threading_CancellationToken_"></a>

### `RouteAsync(Regex, Func<WebRoute, Task>, CancellationToken)`


```csharp
public Task RouteAsync(Regex pattern, Func<WebRoute, Task> handler, CancellationToken cancellationToken = default)
```


Adds a network route for the URLs &lt;code class="paramref"&gt;pattern&lt;/code&gt; matches. See `CancellationToken)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `handler` | [Func&lt;WebRoute, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L240)

<a id="E2E_Browser_SetCookiesAsync_"></a>
<a id="E2E_Browser_SetCookiesAsync_System_Collections_Generic_IReadOnlyList_E2E_Engine_BrowserCookie__System_Threading_CancellationToken_"></a>

### `SetCookiesAsync(IReadOnlyList<BrowserCookie>, CancellationToken)`


```csharp
public Task SetCookiesAsync(IReadOnlyList<BrowserCookie> cookies, CancellationToken cancellationToken = default)
```


Sets cookies. Each target URL, or the origin a domain cookie is sent to, must pass the URL rule. A relative url resolves against the base URL.
A cookie URL that does not parse, or uses a scheme other than `http:` or `https:` (`about:blank` included),
is `POLICY_DENIED`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cookies` | [IReadOnlyList&lt;BrowserCookie&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L294)

<a id="E2E_Browser_SetViewportAsync_"></a>
<a id="E2E_Browser_SetViewportAsync_System_Int32_System_Int32_System_Threading_CancellationToken_"></a>

### SetViewportAsync(int, int, CancellationToken)


```csharp
public Task SetViewportAsync(int width, int height, CancellationToken cancellationToken = default)
```


Sets the viewport size for the rest of the attempt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L336)

<a id="E2E_Browser_TitleAsync_"></a>
<a id="E2E_Browser_TitleAsync_System_Threading_CancellationToken_"></a>

### TitleAsync(CancellationToken)


```csharp
public Task<string> TitleAsync(CancellationToken cancellationToken = default)
```


Returns the current title. A page that does not answer within the action timeout fails with `OPERATION_TIMEOUT`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L132)

<a id="E2E_Browser_UnrouteAllAsync_"></a>
<a id="E2E_Browser_UnrouteAllAsync_System_Threading_CancellationToken_"></a>

### UnrouteAllAsync(CancellationToken)


```csharp
public Task UnrouteAllAsync(CancellationToken cancellationToken = default)
```


Removes every route added through this browser. The configured site headers stay.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L261)

<a id="E2E_Browser_UnrouteAsync_"></a>
<a id="E2E_Browser_UnrouteAsync_System_String_System_Threading_CancellationToken_"></a>

### UnrouteAsync(string, CancellationToken)


```csharp
public Task UnrouteAsync(string pattern, CancellationToken cancellationToken = default)
```


Removes every route added with the same glob.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L247)

<a id="E2E_Browser_UnrouteAsync_System_Text_RegularExpressions_Regex_System_Threading_CancellationToken_"></a>

### UnrouteAsync(Regex, CancellationToken)


```csharp
public Task UnrouteAsync(Regex pattern, CancellationToken cancellationToken = default)
```


Removes every route added with a regex of the same source and options.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L254)

<a id="E2E_Browser_UrlAsync_"></a>
<a id="E2E_Browser_UrlAsync_System_Threading_CancellationToken_"></a>

### UrlAsync(CancellationToken)


```csharp
public Task<string> UrlAsync(CancellationToken cancellationToken = default)
```


Returns the current URL.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L120)

<a id="E2E_Browser_WaitForDownloadAsync_"></a>
<a id="E2E_Browser_WaitForDownloadAsync_System_Func_System_Threading_Tasks_Task__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### `WaitForDownloadAsync(Func<Task>, TimeSpan?, CancellationToken)`


```csharp
public Task<BrowserDownload> WaitForDownloadAsync(Func<Task> trigger, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Runs &lt;code class="paramref"&gt;trigger&lt;/code&gt; and waits for the download it starts, within &lt;code class="paramref"&gt;timeout&lt;/code&gt; (the action timeout by
default). The file is kept under the attempt's `downloads` artifact folder, as an artifact of this step. A text download
(`text/*` or `application/json`) is rewritten in place through the session's secrets when it holds any, and the report
labels it `complete`; any other download stays as the app served it, `incomplete`. A trigger that
throws fails the call with its own error, and one that starts no download fails with `ACTION_FAILED`, naming the wait and
how long the trigger took.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `trigger` | [Func&lt;Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BrowserDownload&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L397)

<a id="E2E_Browser_WaitForResponseAsync_"></a>
<a id="E2E_Browser_WaitForResponseAsync_System_String_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### WaitForResponseAsync(string, TimeSpan?, CancellationToken)


```csharp
public Task<WebResponse> WaitForResponseAsync(string pattern, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for a response whose complete URL matches the glob &lt;code class="paramref"&gt;pattern&lt;/code&gt;:
`*` matches within one path segment, `**` crosses `/`, `?`
matches one character, and `\` escapes the next one. The timeout bounds
the match only, and defaults to the action timeout.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;WebResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L212)

<a id="E2E_Browser_WaitForResponseAsync_System_Text_RegularExpressions_Regex_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### WaitForResponseAsync(Regex, TimeSpan?, CancellationToken)


```csharp
public Task<WebResponse> WaitForResponseAsync(Regex pattern, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for a response whose URL matches &lt;code class="paramref"&gt;pattern&lt;/code&gt;. The timeout bounds the match only, and defaults to the action timeout.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;WebResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L219)

<a id="E2E_Browser_WaitForURLAsync_"></a>
<a id="E2E_Browser_WaitForURLAsync_System_String_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### WaitForURLAsync(string, TimeSpan?, CancellationToken)


```csharp
public Task WaitForURLAsync(string url, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits until the current URL equals &lt;code class="paramref"&gt;url&lt;/code&gt;, resolved
against the base URL, as `CancellationToken)`.
The default timeout is the assertion timeout.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L140)

<a id="E2E_Browser_WaitForURLAsync_System_Text_RegularExpressions_Regex_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### WaitForURLAsync(Regex, TimeSpan?, CancellationToken)


```csharp
public Task WaitForURLAsync(Regex pattern, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits until the current URL matches &lt;code class="paramref"&gt;pattern&lt;/code&gt;. The default timeout is the assertion timeout.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L151)
