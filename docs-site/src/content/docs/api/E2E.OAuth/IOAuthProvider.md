---
title: "IOAuthProvider"
description: "The contract between a subscription login and the model clients: a provider signs the user in, refreshes what the login returned, and adjusts one API request..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IOAuthProvider"
  hidden: true
---

<a id="E2E_OAuth_IOAuthProvider"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public interface IOAuthProvider
```


The contract between a subscription login and the model clients: a provider signs the user in, refreshes
what the login returned, and adjusts one API request the vendor's way. The credentials file, the handler that
swaps the token in, and the models are shared.

**Derived:** [CodexProvider](/api/E2E.OAuth/CodexProvider/), [CopilotProvider](/api/E2E.OAuth/CopilotProvider/), [OpenCodeConsoleProvider](/api/E2E.OAuth/OpenCodeConsoleProvider/), [XaiProvider](/api/E2E.OAuth/XaiProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs)

## Properties

<a id="E2E_OAuth_IOAuthProvider_ApiKeyEnv_"></a>
<a id="E2E_OAuth_IOAuthProvider_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
string? ApiKeyEnv { get; }
```


The variable whose API key the provider's calls and model list read in place of the stored login.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_OAuth_IOAuthProvider_Id_"></a>
<a id="E2E_OAuth_IOAuthProvider_Id"></a>

### Id


```csharp
string Id { get; }
```


The id in the credentials file and on the command line: `openai`, `github-copilot`, `opencode-console`, `spacexai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_OAuth_IOAuthProvider_Name_"></a>
<a id="E2E_OAuth_IOAuthProvider_Name"></a>

### Name


```csharp
string Name { get; }
```


The vendor's name for messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_OAuth_IOAuthProvider_ListModelsAsync_"></a>
<a id="E2E_OAuth_IOAuthProvider_ListModelsAsync_System_Net_Http_HttpClient_System_Threading_CancellationToken_"></a>

### ListModelsAsync(HttpClient, CancellationToken)


```csharp
Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken)
```


The models the subscription serves, asked of the vendor through &lt;code class="paramref"&gt;http&lt;/code&gt;, which carries the login.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SubscriptionModel&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_IOAuthProvider_LoginAsync_"></a>
<a id="E2E_OAuth_IOAuthProvider_LoginAsync_E2E_OAuth_OAuthLoginCallbacks_E2E_OAuth_OAuthLoginOptions_System_Threading_CancellationToken_"></a>

### LoginAsync(OAuthLoginCallbacks, OAuthLoginOptions, CancellationToken)


```csharp
Task<OAuthCredentials> LoginAsync(OAuthLoginCallbacks callbacks, OAuthLoginOptions options, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `callbacks` | [OAuthLoginCallbacks](/api/E2E.OAuth/OAuthLoginCallbacks/) |  |
| `options` | [OAuthLoginOptions](/api/E2E.OAuth/OAuthLoginOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_IOAuthProvider_PrepareAsync_"></a>
<a id="E2E_OAuth_IOAuthProvider_PrepareAsync_System_Net_Http_HttpRequestMessage_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### PrepareAsync(HttpRequestMessage, OAuthCredentials, CancellationToken)


```csharp
Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken)
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

<a id="E2E_OAuth_IOAuthProvider_RefreshAsync_"></a>
<a id="E2E_OAuth_IOAuthProvider_RefreshAsync_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### RefreshAsync(OAuthCredentials, CancellationToken)


```csharp
Task<OAuthCredentials> RefreshAsync(OAuthCredentials credentials, CancellationToken cancellationToken)
```


Renews the credentials, or throws [LoginRequired](/api/E2E.OAuth/OAuthException/#E2E_OAuth_OAuthException_LoginRequired) when the user has to sign in again.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
