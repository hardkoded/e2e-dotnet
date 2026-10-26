---
title: "Subscriptions"
description: "Subscription logins as agent models, upstream's chatgpt(), copilot(), grok(), and opencodeConsole(). Each reads the login e2e login stored (or E2EOAUTHCREDEN..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Subscriptions"
  hidden: true
---

<a id="E2E_OAuth_Subscriptions"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public static class Subscriptions
```


Subscription logins as agent models, upstream's `chatgpt()`, `copilot()`, `grok()`, and
`opencodeConsole()`. Each reads the login `e2e login` stored (or `E2E_OAUTH_CREDENTIALS`), refreshes
it as needed, and touches nothing until the first call. Use API keys in CI.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L15)

## Methods

<a id="E2E_OAuth_Subscriptions_ChatGpt_"></a>
<a id="E2E_OAuth_Subscriptions_ChatGpt_System_String_E2E_OAuth_ICredentialStore_"></a>

### ChatGpt(string, ICredentialStore?)


```csharp
public static OpenAiResponsesModel ChatGpt(string model, ICredentialStore? store = null)
```


A ChatGPT Plus/Pro subscription over the Codex backend. `e2e models openai` lists the ids the plan serves.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |

**Returns:** [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L18)

<a id="E2E_OAuth_Subscriptions_Client_"></a>
<a id="E2E_OAuth_Subscriptions_Client_E2E_OAuth_IOAuthProvider_E2E_OAuth_ICredentialStore_System_String_System_Net_Http_HttpMessageHandler_"></a>

### Client(IOAuthProvider, ICredentialStore?, string?, HttpMessageHandler?)


```csharp
public static HttpClient Client(IOAuthProvider provider, ICredentialStore? store = null, string? loginHint = null, HttpMessageHandler? inner = null)
```


An [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) that carries &lt;code class="paramref"&gt;provider&lt;/code&gt;'s stored login on every request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `provider` | [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |
| `loginHint` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [HttpMessageHandler](https://learn.microsoft.com/dotnet/api/system.net.http.httpmessagehandler) |  |

**Returns:** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L66)

<a id="E2E_OAuth_Subscriptions_Copilot_"></a>
<a id="E2E_OAuth_Subscriptions_Copilot_System_String_E2E_OAuth_ICredentialStore_"></a>

### Copilot(string, ICredentialStore?)


```csharp
public static CopilotModel Copilot(string model, ICredentialStore? store = null)
```


A GitHub Copilot subscription. Copilot serves most models over chat completions and some only over its
Responses API; the first call reads the plan's model listing to pick. An enterprise login routes to its host.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |

**Returns:** [CopilotModel](/api/E2E.OAuth/CopilotModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L36)

<a id="E2E_OAuth_Subscriptions_Grok_"></a>
<a id="E2E_OAuth_Subscriptions_Grok_System_String_E2E_OAuth_ICredentialStore_"></a>

### Grok(string, ICredentialStore?)


```csharp
public static OpenAiCompatibleModel Grok(string model, ICredentialStore? store = null)
```


A SuperGrok or X Premium+ subscription over the ordinary SpaceXAI API.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |

**Returns:** [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L42)

<a id="E2E_OAuth_Subscriptions_OpenCodeConsole_"></a>
<a id="E2E_OAuth_Subscriptions_OpenCodeConsole_System_String_E2E_OAuth_ICredentialStore_"></a>

### OpenCodeConsole(string, ICredentialStore?)


```csharp
public static OpenCodeConsoleModel OpenCodeConsole(string model, ICredentialStore? store = null)
```


An OpenCode Console workspace: OpenCode Zen for a bare id, OpenCode Go for a `go/` id. The first call
reads the workspace config for the model's protocol. `OPENCODE_API_KEY` (a Console service account API key
or an OpenCode Go subscription API key) replaces the stored login when set.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |

**Returns:** [OpenCodeConsoleModel](/api/E2E.OAuth/OpenCodeConsoleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L54)
