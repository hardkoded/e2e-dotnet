---
title: "OpenCodeConsoleProvider"
description: "OpenCode Zen and Go through an OpenCode Console sign-in. Console speaks RFC 8628 device authorization; approving the code binds the login to one workspace, a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenCodeConsoleProvider"
  hidden: true
---

<a id="E2E_OAuth_OpenCodeConsoleProvider"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OpenCodeConsoleProvider : IOAuthProvider
```


OpenCode Zen and Go through an OpenCode Console sign-in. Console speaks RFC 8628 device authorization;
approving the code binds the login to one workspace, and its token is a bearer for both the Console API and
inference. Refresh tokens rotate. The workspace config says which models are served and over which protocol:
Zen is its `opencode` provider, Go `opencode-go`. Go ids take a `go/` prefix here.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L17)

## Constructors

<a id="E2E_OAuth_OpenCodeConsoleProvider__ctor_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider__ctor_System_Net_Http_HttpClient_System_String_"></a>

### OpenCodeConsoleProvider(HttpClient?, string)


```csharp
public OpenCodeConsoleProvider(HttpClient? http = null, string consoleUrl = "https://opencode.ai/console")
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) | Sends the login's own requests. Unset, a shared client. |
| `consoleUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Test seam. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L32)

## Fields

<a id="E2E_OAuth_OpenCodeConsoleProvider_DefaultConsoleUrl"></a>

### DefaultConsoleUrl


```csharp
public const string DefaultConsoleUrl = "https://opencode.ai/console"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L25)

<a id="E2E_OAuth_OpenCodeConsoleProvider_GoPrefix"></a>

### GoPrefix


```csharp
public const string GoPrefix = "go/"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L24)

<a id="E2E_OAuth_OpenCodeConsoleProvider_InferenceUrl"></a>

### InferenceUrl


```csharp
public const string InferenceUrl = "https://opencode.ai/inference"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L26)

## Properties

<a id="E2E_OAuth_OpenCodeConsoleProvider_ApiKeyEnv_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string? ApiKeyEnv { get; }
```


The variable whose API key the provider's calls and model list read in place of the stored login.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L42)

<a id="E2E_OAuth_OpenCodeConsoleProvider_Id_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_Id"></a>

### Id


```csharp
public string Id { get; }
```


The id in the credentials file and on the command line: `openai`, `github-copilot`, `opencode-console`, `spacexai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L38)

<a id="E2E_OAuth_OpenCodeConsoleProvider_Name_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_Name"></a>

### Name


```csharp
public string Name { get; }
```


The vendor's name for messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L40)

## Methods

<a id="E2E_OAuth_OpenCodeConsoleProvider_ListModelsAsync_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_ListModelsAsync_System_Net_Http_HttpClient_System_Threading_CancellationToken_"></a>

### ListModelsAsync(HttpClient, CancellationToken)


```csharp
public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken)
```


Leaves out disabled models and Zen's free ones, which only OpenCode's own clients may use.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SubscriptionModel&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L111)

<a id="E2E_OAuth_OpenCodeConsoleProvider_LoginAsync_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_LoginAsync_E2E_OAuth_OAuthLoginCallbacks_E2E_OAuth_OAuthLoginOptions_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L46)

<a id="E2E_OAuth_OpenCodeConsoleProvider_PrepareAsync_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_PrepareAsync_System_Net_Http_HttpRequestMessage_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### PrepareAsync(HttpRequestMessage, OAuthCredentials, CancellationToken)


```csharp
public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken)
```


Names the workspace as each API expects it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) |  |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L97)

<a id="E2E_OAuth_OpenCodeConsoleProvider_RefreshAsync_"></a>
<a id="E2E_OAuth_OpenCodeConsoleProvider_RefreshAsync_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/OpenCodeConsoleProvider.cs#L78)
