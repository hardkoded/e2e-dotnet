---
title: "XaiProvider"
description: "SuperGrok and X Premium+ through SpaceXAI's OAuth server: RFC 8628 device authorization for the Grok CLI's public client. The token is a plain bearer against..."
editUrl: false
prev: false
next: false
sidebar:
  label: "XaiProvider"
  hidden: true
---

<a id="E2E_OAuth_XaiProvider"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class XaiProvider : IOAuthProvider
```


SuperGrok and X Premium+ through SpaceXAI's OAuth server: RFC 8628 device authorization for the Grok CLI's
public client. The token is a plain bearer against the ordinary SpaceXAI API. Refresh tokens rotate.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L13)

## Constructors

<a id="E2E_OAuth_XaiProvider__ctor_"></a>
<a id="E2E_OAuth_XaiProvider__ctor_System_Net_Http_HttpClient_System_String_System_String_System_String_"></a>

### XaiProvider(HttpClient?, string, string, string)


```csharp
public XaiProvider(HttpClient? http = null, string referrer = "e2e", string issuer = "https://auth.x.ai", string modelsUrl = "https://api.x.ai/v1/language-models")
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) | Sends the login's own requests. Unset, a shared client. |
| `referrer` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The `referrer` the device request names: your product. |
| `issuer` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Test seam. |
| `modelsUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Test seam. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L30)

## Fields

<a id="E2E_OAuth_XaiProvider_ApiUrl"></a>

### ApiUrl


```csharp
public const string ApiUrl = "https://api.x.ai/v1"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L19)

## Properties

<a id="E2E_OAuth_XaiProvider_Id_"></a>
<a id="E2E_OAuth_XaiProvider_Id"></a>

### Id


```csharp
public string Id { get; }
```


The id in the credentials file and on the command line: `openai`, `github-copilot`, `opencode-console`, `spacexai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L38)

<a id="E2E_OAuth_XaiProvider_Name_"></a>
<a id="E2E_OAuth_XaiProvider_Name"></a>

### Name


```csharp
public string Name { get; }
```


The vendor's name for messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L40)

## Methods

<a id="E2E_OAuth_XaiProvider_ListModelsAsync_"></a>
<a id="E2E_OAuth_XaiProvider_ListModelsAsync_System_Net_Http_HttpClient_System_Threading_CancellationToken_"></a>

### ListModelsAsync(HttpClient, CancellationToken)


```csharp
public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken)
```


The language models the token reaches; aliases are listed with their target.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SubscriptionModel&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L76)

<a id="E2E_OAuth_XaiProvider_LoginAsync_"></a>
<a id="E2E_OAuth_XaiProvider_LoginAsync_E2E_OAuth_OAuthLoginCallbacks_E2E_OAuth_OAuthLoginOptions_System_Threading_CancellationToken_"></a>

### LoginAsync(OAuthLoginCallbacks, OAuthLoginOptions, CancellationToken)


```csharp
public Task<OAuthCredentials> LoginAsync(OAuthLoginCallbacks callbacks, OAuthLoginOptions options, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `callbacks` | [OAuthLoginCallbacks](/api/E2E.OAuth/OAuthLoginCallbacks/) |  |
| `options` | [OAuthLoginOptions](/api/E2E.OAuth/OAuthLoginOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L42)

<a id="E2E_OAuth_XaiProvider_PrepareAsync_"></a>
<a id="E2E_OAuth_XaiProvider_PrepareAsync_System_Net_Http_HttpRequestMessage_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### PrepareAsync(HttpRequestMessage, OAuthCredentials, CancellationToken)


```csharp
public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken)
```


Adjusts one API request before it goes out. It arrives with the bearer token and the user agent set; the
provider rewrites the URL, adds vendor headers, or changes the body.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) |  |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L70)

<a id="E2E_OAuth_XaiProvider_RefreshAsync_"></a>
<a id="E2E_OAuth_XaiProvider_RefreshAsync_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### RefreshAsync(OAuthCredentials, CancellationToken)


```csharp
public Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken)
```


Renews the credentials, or throws [LoginRequired](/api/E2E.OAuth/OAuthException/#E2E_OAuth_OAuthException_LoginRequired) when the user has to sign in again.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/XaiProvider.cs#L57)
