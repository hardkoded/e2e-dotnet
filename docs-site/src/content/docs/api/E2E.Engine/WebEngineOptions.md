---
title: "WebEngineOptions"
description: "Options of the browser engine: how it drives the app."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebEngineOptions"
  hidden: true
---

<a id="E2E_Engine_WebEngineOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebEngineOptions
```


Options of the browser engine: how it drives the app.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L169)

## Properties

<a id="E2E_Engine_WebEngineOptions_BasicAuth_"></a>
<a id="E2E_Engine_WebEngineOptions_BasicAuth"></a>

### BasicAuth


```csharp
public WebBasicAuth? BasicAuth { get; init; }
```


Basic-auth credentials the browser answers a challenge with, wherever one is issued.

**Value:** [WebBasicAuth](/api/E2E.Engine/WebBasicAuth/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L193)

<a id="E2E_Engine_WebEngineOptions_Browser_"></a>
<a id="E2E_Engine_WebEngineOptions_Browser"></a>

### Browser


```csharp
public IBrowserProvider? Browser { get; init; }
```


Leases hosted browsers from a provider instead of launching one: the engine attaches over CDP the way
[Connect](#E2E_Engine_WebEngineOptions_Connect) does, and adds the lifecycle [Connect](#E2E_Engine_WebEngineOptions_Connect) lacks. It asks the provider for a browser
when one is needed and gives it back when its scope ends, on every exit path. A provider implies Chromium and
cannot be combined with [Connect](#E2E_Engine_WebEngineOptions_Connect). With [Attempt](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Attempt) scope the attempt
rides a persistent context, with the limits of [ReconnectEndpoint](/api/E2E.Engine/WebConnectOptions/#E2E_Engine_WebConnectOptions_ReconnectEndpoint).

**Value:** [IBrowserProvider](/api/E2E.Engine/IBrowserProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L246)

<a id="E2E_Engine_WebEngineOptions_ColorScheme_"></a>
<a id="E2E_Engine_WebEngineOptions_ColorScheme"></a>

### ColorScheme


```csharp
public WebColorScheme? ColorScheme { get; init; }
```


The `prefers-color-scheme` every page reports from its first paint, to its `@media` rules and
to `matchMedia`, as Playwright's own `colorScheme` context option sets it. Defaults to the
browser's own. `Browser.EmulateMediaAsync` changes it for the rest of one attempt.

**Value:** `WebColorScheme?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L217)

<a id="E2E_Engine_WebEngineOptions_Connect_"></a>
<a id="E2E_Engine_WebEngineOptions_Connect"></a>

### Connect


```csharp
public WebConnectOptions? Connect { get; init; }
```


Attach to a remote Chromium over CDP instead of launching a local one.

**Value:** [WebConnectOptions](/api/E2E.Engine/WebConnectOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L237)

<a id="E2E_Engine_WebEngineOptions_Headers_"></a>
<a id="E2E_Engine_WebEngineOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Headers added to every request bound for the app's host (the host of the
session's base URL). Requests to any other host never carry them, so a
secret header stays with the app it unlocks. Names are case-insensitive
and replace a header the page already sends under the same name.
Routing every request turns the browser's HTTP cache off.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L190)

<a id="E2E_Engine_WebEngineOptions_Headless_"></a>
<a id="E2E_Engine_WebEngineOptions_Headless"></a>

### Headless


```csharp
public bool? Headless { get; init; }
```


Runs Chromium without a window. When unset, `E2E_HEADLESS=0` or `false` shows the window; headless otherwise.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L172)

<a id="E2E_Engine_WebEngineOptions_InitScripts_"></a>
<a id="E2E_Engine_WebEngineOptions_InitScripts"></a>

### InitScripts


```csharp
public IReadOnlyList<WebInitScript>? InitScripts { get; init; }
```


Scripts every document runs before the page's own, in every tab and frame, in order.

**Value:** [IReadOnlyList&lt;WebInitScript&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L228)

<a id="E2E_Engine_WebEngineOptions_Locale_"></a>
<a id="E2E_Engine_WebEngineOptions_Locale"></a>

### Locale


```csharp
public string? Locale { get; init; }
```


The locale every attempt's context runs in, a BCP 47 tag such as `de-DE`:
what `navigator.language`, `Intl` formatting, and the `Accept-Language`
header report. Defaults to the browser's own. An `accept-language` entry in
[Headers](#E2E_Engine_WebEngineOptions_Headers) beside it is `INVALID_CONFIG`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L204)

<a id="E2E_Engine_WebEngineOptions_ReducedMotion_"></a>
<a id="E2E_Engine_WebEngineOptions_ReducedMotion"></a>

### ReducedMotion


```csharp
public WebReducedMotion? ReducedMotion { get; init; }
```


The `prefers-reduced-motion` every page reports from its first paint, as Playwright's own
`reducedMotion` context option sets it: [Reduce](/api/E2E.Engine/WebReducedMotion/#E2E_Engine_WebReducedMotion_Reduce) lets an app's own rule
turn its animations off, so none races an assertion. Defaults to the browser's own.
`Browser.EmulateMediaAsync` changes it for the rest of one attempt.

**Value:** `WebReducedMotion?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L225)

<a id="E2E_Engine_WebEngineOptions_Screencast_"></a>
<a id="E2E_Engine_WebEngineOptions_Screencast"></a>

### Screencast


```csharp
public WebScreencastOptions? Screencast { get; init; }
```


How the engine's own screencast records an attempt that records video, when the browser provider does not record
it. Which attempts record is the config's `video`.

**Value:** [WebScreencastOptions](/api/E2E.Engine/WebScreencastOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L234)

<a id="E2E_Engine_WebEngineOptions_TestIdAttribute_"></a>
<a id="E2E_Engine_WebEngineOptions_TestIdAttribute"></a>

### TestIdAttribute


```csharp
public string TestIdAttribute { get; init; }
```


The attribute that carries an element's test id: what `GetByTestId` resolves and [TestId](/api/E2E.Engine/SemanticNode/#E2E_Engine_SemanticNode_TestId) reports.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L181)

<a id="E2E_Engine_WebEngineOptions_TimezoneId_"></a>
<a id="E2E_Engine_WebEngineOptions_TimezoneId"></a>

### TimezoneId


```csharp
public string? TimezoneId { get; init; }
```


The IANA time zone every attempt's context runs in, such as `Europe/Berlin`:
what `Date` and `Intl` resolve local time against. Defaults to the machine's.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L210)

<a id="E2E_Engine_WebEngineOptions_UserAgent_"></a>
<a id="E2E_Engine_WebEngineOptions_UserAgent"></a>

### UserAgent


```csharp
public string? UserAgent { get; init; }
```


The `User-Agent` the browser sends and `navigator.userAgent` reports. Defaults to the browser's own.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L196)

<a id="E2E_Engine_WebEngineOptions_Viewport_"></a>
<a id="E2E_Engine_WebEngineOptions_Viewport"></a>

### Viewport


```csharp
public WebViewport? Viewport { get; init; }
```


Initial viewport of every attempt's page; default 1280 by 720. &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt;
emulates no size: the page fills the browser window, whatever size it has.

**Value:** [WebViewport](/api/E2E.Engine/WebViewport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L178)
