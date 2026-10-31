---
title: "CopilotProvider"
description: "GitHub Copilot. The Copilot API accepts a GitHub sign-in as the bearer, so the login is either the token the GitHub CLI already holds or GitHub's device flow..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CopilotProvider"
  hidden: true
---

<a id="E2E_OAuth_CopilotProvider"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class CopilotProvider : IOAuthProvider
```


GitHub Copilot. The Copilot API accepts a GitHub sign-in as the bearer, so the login is either the token the
GitHub CLI already holds or GitHub's device flow for an OAuth App you register. GitHub user tokens do not
expire; a 401 means signing in again.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs#L1500)

## Constructors

<a id="E2E_OAuth_CopilotProvider__ctor_"></a>
<a id="E2E_OAuth_CopilotProvider__ctor_System_Net_Http_HttpClient_System_String_System_Func_System_String_System_Threading_CancellationToken_System_Threading_Tasks_Task_System_String___"></a>

### `CopilotProvider(HttpClient?, string?, Func<string?, CancellationToken, Task<string?>>?)`


```csharp
public CopilotProvider(HttpClient? http = null, string? githubUrl = null, Func<string?, CancellationToken, Task<string?>>? githubCliToken = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) | Sends the login's own requests. Unset, a shared client. |
| `githubUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Test seam: the GitHub host the device flow talks to. |
| `githubCliToken` | [Func&lt;string, CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3) | Test seam: how the GitHub CLI's token is read for a host. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L28)

## Fields

<a id="E2E_OAuth_CopilotProvider_ApiUrl"></a>

### ApiUrl


```csharp
public const string ApiUrl = "https://api.githubcopilot.com"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L19)

## Properties

<a id="E2E_OAuth_CopilotProvider_Id_"></a>
<a id="E2E_OAuth_CopilotProvider_Id"></a>

### Id


```csharp
public string Id { get; }
```


The id in the credentials file and on the command line: `openai`, `github-copilot`, `opencode-console`, `spacexai`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L35)

<a id="E2E_OAuth_CopilotProvider_Name_"></a>
<a id="E2E_OAuth_CopilotProvider_Name"></a>

### Name


```csharp
public string Name { get; }
```


The vendor's name for messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L37)

## Methods

<a id="E2E_OAuth_CopilotProvider_BaseUrl_"></a>
<a id="E2E_OAuth_CopilotProvider_BaseUrl_System_String_"></a>

### BaseUrl(string?)


```csharp
public static string BaseUrl(string? enterpriseUrl)
```


The Copilot API base for github.com or an enterprise host.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `enterpriseUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L172)

<a id="E2E_OAuth_CopilotProvider_EnterpriseHost_"></a>
<a id="E2E_OAuth_CopilotProvider_EnterpriseHost_System_String_"></a>

### EnterpriseHost(string)


```csharp
public static string EnterpriseHost(string value)
```


The bare host of a GitHub Enterprise URL. The bearer token goes to a host derived from it, so anything but a
plain hostname is refused rather than guessed at.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L181)

<a id="E2E_OAuth_CopilotProvider_ListModelsAsync_"></a>
<a id="E2E_OAuth_CopilotProvider_ListModelsAsync_System_Net_Http_HttpClient_System_Threading_CancellationToken_"></a>

### ListModelsAsync(HttpClient, CancellationToken)


```csharp
public Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(HttpClient http, CancellationToken cancellationToken)
```


The chat models the plan serves. A model it cannot call is listed but marked: one the plan has not enabled,
or one Copilot serves only over APIs these clients do not speak.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SubscriptionModel&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L103)

<a id="E2E_OAuth_CopilotProvider_LoginAsync_"></a>
<a id="E2E_OAuth_CopilotProvider_LoginAsync_E2E_OAuth_OAuthLoginCallbacks_E2E_OAuth_OAuthLoginOptions_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L39)

<a id="E2E_OAuth_CopilotProvider_PrepareAsync_"></a>
<a id="E2E_OAuth_CopilotProvider_PrepareAsync_System_Net_Http_HttpRequestMessage_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### PrepareAsync(HttpRequestMessage, OAuthCredentials, CancellationToken)


```csharp
public Task PrepareAsync(HttpRequestMessage request, OAuthCredentials credentials, CancellationToken cancellationToken)
```


Routes an enterprise login at its host and adds Copilot's headers: who initiated the turn, and that images are present.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) |  |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L79)

<a id="E2E_OAuth_CopilotProvider_RefreshAsync_"></a>
<a id="E2E_OAuth_CopilotProvider_RefreshAsync_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Providers/CopilotProvider.cs#L73)
