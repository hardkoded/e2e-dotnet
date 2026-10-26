---
title: "OAuthHandler"
description: "The handler a subscription model's HttpClient sends through. Per request it reads the stored credentials, refreshes them ahead of expiry, replaces any API ke..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthHandler"
  hidden: true
---

<a id="E2E_OAuth_OAuthHandler"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OAuthHandler : DelegatingHandler, IDisposable
```


The handler a subscription model's [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) sends through. Per request it reads the stored
credentials, refreshes them ahead of expiry, replaces any API key header with the bearer token, and lets the
provider adjust the request. On a 401 it refreshes once and retries, or, with nothing to refresh, reports that
the user signs in again. One refresh serves every model and every concurrent call over the same store,
because vendors that rotate refresh tokens reject the second concurrent refresh.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [HttpMessageHandler](https://learn.microsoft.com/dotnet/api/system.net.http.httpmessagehandler) → [DelegatingHandler](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler)

**Implements:** [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthHandler.cs#L19)

## Constructors

<a id="E2E_OAuth_OAuthHandler__ctor_"></a>
<a id="E2E_OAuth_OAuthHandler__ctor_E2E_OAuth_IOAuthProvider_E2E_OAuth_ICredentialStore_System_String_System_Net_Http_HttpMessageHandler_"></a>

### OAuthHandler(IOAuthProvider, ICredentialStore, string?, HttpMessageHandler?)


```csharp
public OAuthHandler(IOAuthProvider provider, ICredentialStore store, string? loginHint = null, HttpMessageHandler? inner = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `provider` | [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |
| `loginHint` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [HttpMessageHandler](https://learn.microsoft.com/dotnet/api/system.net.http.httpmessagehandler) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthHandler.cs#L37)

## Methods

<a id="E2E_OAuth_OAuthHandler_SendAsync_"></a>
<a id="E2E_OAuth_OAuthHandler_SendAsync_System_Net_Http_HttpRequestMessage_System_Threading_CancellationToken_"></a>

### SendAsync(HttpRequestMessage, CancellationToken)


```csharp
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
```


Sends an HTTP request to the inner handler to send to the server as an asynchronous operation.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) | The HTTP request message to send to the server. |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) | A cancellation token to cancel operation. |

**Returns:** [Task&lt;HttpResponseMessage&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1) — The task object representing the asynchronous operation.

**Exceptions**

| Type | Condition |
| --- | --- |
| [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception) | The &lt;code class="paramref"&gt;request&lt;/code&gt; was &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt;. |
| [OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception) | The cancellation token was canceled. This exception is stored into the returned task. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthHandler.cs#L48)
