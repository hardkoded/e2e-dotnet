---
title: "WebRoute"
description: "One request a CancellationToken) handler intercepted. Take exactly one decision. Each validates its options before it decides, so a rejected option leaves th..."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebRoute"
  hidden: true
---

<a id="E2E_WebRoute"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class WebRoute
```


One request a `CancellationToken)`
handler intercepted. Take exactly one decision. Each validates its options before
it decides, so a rejected option leaves the request undecided, and the request is aborted.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L579)

## Properties

<a id="E2E_WebRoute_Request_"></a>
<a id="E2E_WebRoute_Request"></a>

### Request


```csharp
public WebRouteRequest Request { get; }
```


The intercepted request.

**Value:** [WebRouteRequest](/api/E2E.Engine/WebRouteRequest/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L597)

## Methods

<a id="E2E_WebRoute_AbortAsync_"></a>
<a id="E2E_WebRoute_AbortAsync"></a>

### AbortAsync()


```csharp
public Task AbortAsync()
```


Aborts the request.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L637)

<a id="E2E_WebRoute_ContinueAsync_"></a>
<a id="E2E_WebRoute_ContinueAsync_E2E_Engine_RouteContinueOverrides_"></a>

### ContinueAsync(RouteContinueOverrides?)


```csharp
public Task ContinueAsync(RouteContinueOverrides? overrides = null)
```


Sends the request to the network once, skipping every other route.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `overrides` | [RouteContinueOverrides](/api/E2E.Engine/RouteContinueOverrides/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L622)

<a id="E2E_WebRoute_FallbackAsync_"></a>
<a id="E2E_WebRoute_FallbackAsync"></a>

### FallbackAsync()


```csharp
public Task FallbackAsync()
```


Hands the request to the route registered before this one, or the network when none matches.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L630)

<a id="E2E_WebRoute_FetchAsync_"></a>
<a id="E2E_WebRoute_FetchAsync"></a>

### FetchAsync()


```csharp
public Task<WebResponse> FetchAsync()
```


Reads the upstream response without deciding the route or following redirects, under the action
timeout. The site's configured headers are sent. A redirect comes back unfollowed, because
following it could take the request past the navigation rule. Decide the route afterwards
with [FulfillAsync(RouteFulfillResponse)](#E2E_WebRoute_FulfillAsync_E2E_Engine_RouteFulfillResponse_), [ContinueAsync(RouteContinueOverrides)](#E2E_WebRoute_ContinueAsync_E2E_Engine_RouteContinueOverrides_), [FallbackAsync()](#E2E_WebRoute_FallbackAsync), or [AbortAsync()](#E2E_WebRoute_AbortAsync).

**Returns:** [Task&lt;WebResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L607)

<a id="E2E_WebRoute_FulfillAsync_"></a>
<a id="E2E_WebRoute_FulfillAsync_E2E_Engine_RouteFulfillResponse_"></a>

### FulfillAsync(RouteFulfillResponse)


```csharp
public Task FulfillAsync(RouteFulfillResponse response)
```


Answers the request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `response` | [RouteFulfillResponse](/api/E2E.Engine/RouteFulfillResponse/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L614)
