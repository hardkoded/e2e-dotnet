---
title: "CodexProvider"
description: "ChatGPT Plus/Pro through the Codex OAuth client: PKCE authorization code against auth.openai.com with a local callback on port 1455, or OpenAI's user-code fl..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CodexProvider"
  hidden: true
---

<a id="E2E_OAuth_CodexProvider"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class CodexProvider : IOAuthProvider
```


ChatGPT Plus/Pro through the Codex OAuth client: PKCE authorization code against `auth.openai.com` with a
local callback on port 1455, or OpenAI's user-code flow for a machine without a browser. Requests go to the
Codex backend, which speaks the Responses API, requires `store: false`, and answers only as a stream.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L18)

## Constructors

<a id="E2E_OAuth_CodexProvider__ctor_"></a>
<a id="E2E_OAuth_CodexProvider__ctor_System_Net_Http_HttpClient_System_String_System_String_System_String_System_Int32_System_Nullable_System_TimeSpan__"></a>

### CodexProvider(HttpClient?, string, string, string, int, TimeSpan?)


```csharp
public CodexProvider(HttpClient? http = null, string originator = "e2e", string issuer = "https://auth.openai.com", string apiUrl = "https://chatgpt.com/backend-api/codex/responses", int callbackPort = 1455, TimeSpan? loginTimeout = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) | Sends the login's own requests. Unset, a shared client. |
| `originator` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The `originator` the authorization page and requests carry: your product's name. |
| `issuer` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Test seam. |
| `apiUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Test seam. |
| `callbackPort` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Test seam. |
| `loginTimeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) | Test seam. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L47)

## Fields

<a id="E2E_OAuth_CodexProvider_DefaultApiUrl"></a>

### DefaultApiUrl


```csharp
public const string DefaultApiUrl = "https://chatgpt.com/backend-api/codex/responses"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L32)

## Properties

<a id="E2E_OAuth_CodexProvider_Id_"></a>
<a id="E2E_OAuth_CodexProvider_Id"></a>

### Id


```csharp
public string Id { get; }
```


The id in the credentials file and on the command line: `openai`, `github-copilot`, `opencode-console`, `spacexai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L63)

<a id="E2E_OAuth_CodexProvider_Name_"></a>
<a id="E2E_OAuth_CodexProvider_Name"></a>

### Name


```csharp
public string Name { get; }
```


The vendor's name for messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L65)

## Methods

<a id="E2E_OAuth_CodexProvider_ListModelsAsync_"></a>
<a id="E2E_OAuth_CodexProvider_ListModelsAsync_System_Net_Http_HttpClient_System_Threading_CancellationToken_"></a>

### ListModelsAsync(HttpClient, CancellationToken)


```csharp
public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken)
```


The models the Codex backend serves this subscription, `/models` next to `/responses`. Hidden ones are listed, marked.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SubscriptionModel&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L147)

<a id="E2E_OAuth_CodexProvider_LoginAsync_"></a>
<a id="E2E_OAuth_CodexProvider_LoginAsync_E2E_OAuth_OAuthLoginCallbacks_E2E_OAuth_OAuthLoginOptions_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L67)

<a id="E2E_OAuth_CodexProvider_PrepareAsync_"></a>
<a id="E2E_OAuth_CodexProvider_PrepareAsync_System_Net_Http_HttpRequestMessage_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### PrepareAsync(HttpRequestMessage, OAuthCredentials, CancellationToken)


```csharp
public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken)
```


Routes a Responses request at the Codex backend with the body the Codex CLI sends: no server-side storage,
encrypted reasoning carried between turns, always streamed. The model folds the stream back.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) |  |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L94)

<a id="E2E_OAuth_CodexProvider_RefreshAsync_"></a>
<a id="E2E_OAuth_CodexProvider_RefreshAsync_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CodexProvider.cs#L77)
