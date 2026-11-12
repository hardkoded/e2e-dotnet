---
title: "IBrowserRoute"
description: "One intercepted request, as an engine hands it to a route handler. The harness has already validated each decision and takes at most one."
editUrl: false
prev: false
next: false
sidebar:
  label: "IBrowserRoute"
  hidden: true
---

<a id="E2E_Engine_IBrowserRoute"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IBrowserRoute
```


One intercepted request, as an engine hands it to a route handler. The harness
has already validated each decision and takes at most one.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs)

## Properties

<a id="E2E_Engine_IBrowserRoute_Request_"></a>
<a id="E2E_Engine_IBrowserRoute_Request"></a>

### Request


```csharp
WebRouteRequest Request { get; }
```


**Value:** [WebRouteRequest](/api/E2E.Engine/WebRouteRequest/)

## Methods

<a id="E2E_Engine_IBrowserRoute_AbortAsync_"></a>
<a id="E2E_Engine_IBrowserRoute_AbortAsync"></a>

### AbortAsync()


```csharp
Task AbortAsync()
```


**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserRoute_ContinueAsync_"></a>
<a id="E2E_Engine_IBrowserRoute_ContinueAsync_E2E_Engine_RouteContinueOverrides_"></a>

### ContinueAsync(RouteContinueOverrides)


```csharp
Task ContinueAsync(RouteContinueOverrides overrides)
```


Sends the request to the network, skipping every other route. The configured headers are added on top of the request headers.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `overrides` | [RouteContinueOverrides](/api/E2E.Engine/RouteContinueOverrides/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserRoute_FallbackAsync_"></a>
<a id="E2E_Engine_IBrowserRoute_FallbackAsync"></a>

### FallbackAsync()


```csharp
Task FallbackAsync()
```


Hands the request to the route registered before this one, or the network when none matches.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserRoute_FetchAsync_"></a>
<a id="E2E_Engine_IBrowserRoute_FetchAsync_System_TimeSpan_System_Threading_CancellationToken_"></a>

### FetchAsync(TimeSpan, CancellationToken)


```csharp
Task<BrowserResponse> FetchAsync(TimeSpan timeout, CancellationToken cancellationToken)
```


Reads the upstream response without deciding the route or following redirects. The configured headers
are added on top of the request headers. Bounded by &lt;code class="paramref"&gt;timeout&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BrowserResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserRoute_FulfillAsync_"></a>
<a id="E2E_Engine_IBrowserRoute_FulfillAsync_E2E_Engine_RouteFulfillResponse_"></a>

### FulfillAsync(RouteFulfillResponse)


```csharp
Task FulfillAsync(RouteFulfillResponse response)
```


Answers the request. [Status](/api/E2E.Engine/RouteFulfillResponse/#E2E_Engine_RouteFulfillResponse_Status) is set, and [Json](/api/E2E.Engine/RouteFulfillResponse/#E2E_Engine_RouteFulfillResponse_Json) has become a [Body](/api/E2E.Engine/RouteFulfillResponse/#E2E_Engine_RouteFulfillResponse_Body).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `response` | [RouteFulfillResponse](/api/E2E.Engine/RouteFulfillResponse/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
