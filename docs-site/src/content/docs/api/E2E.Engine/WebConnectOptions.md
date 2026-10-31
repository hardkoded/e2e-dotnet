---
title: "WebConnectOptions"
description: "Attach to a remote Chromium over CDP instead of launching one. Supplying ReconnectEndpoint opts into a persistent context instead."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebConnectOptions"
  hidden: true
---

<a id="E2E_Engine_WebConnectOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebConnectOptions
```


Attach to a remote Chromium over CDP instead of launching one. Supplying
[ReconnectEndpoint](#E2E_Engine_WebConnectOptions_ReconnectEndpoint) opts into a persistent context instead.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L151)

## Properties

<a id="E2E_Engine_WebConnectOptions_CdpEndpoint_"></a>
<a id="E2E_Engine_WebConnectOptions_CdpEndpoint"></a>

### CdpEndpoint


```csharp
public required Func<CancellationToken, Task<string>> CdpEndpoint { get; init; }
```


Resolves the CDP endpoint (a `ws://`, `wss://`, or `http://` DevTools URL).
Called when each attempt starts, so a hosted endpoint provisioned per run can be used.
With [ReconnectEndpoint](#E2E_Engine_WebConnectOptions_ReconnectEndpoint), each call must provision a fresh, dedicated browser.

**Value:** [Func&lt;CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L158)

<a id="E2E_Engine_WebConnectOptions_ReconnectEndpoint_"></a>
<a id="E2E_Engine_WebConnectOptions_ReconnectEndpoint"></a>

### ReconnectEndpoint


```csharp
public Func<CancellationToken, Task<string>>? ReconnectEndpoint { get; init; }
```


Opts into a dedicated persistent remote context. [CdpEndpoint](#E2E_Engine_WebConnectOptions_CdpEndpoint) provisions
a fresh browser at each attempt start; this resolver reconnects to that
same browser after a transport drop. Called once before the next
operation, within its budget. The original browser and page must survive.
Dispatched operations are never retried. The host owns browser cleanup.
Context replacement, headers, basicAuth, userAgent, locale, and timezoneId are unavailable in this mode.

**Value:** [Func&lt;CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L168)
