---
title: "OAuthProviders"
description: "The built-in subscription providers by id, and what e2e login, logout, and models do with them."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthProviders"
  hidden: true
---

<a id="E2E_OAuth_OAuthProviders"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public static class OAuthProviders
```


The built-in subscription providers by id, and what `e2e login`, `logout`, and `models` do with them.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L73)

## Properties

<a id="E2E_OAuth_OAuthProviders_Ids_"></a>
<a id="E2E_OAuth_OAuthProviders_Ids"></a>

### Ids


```csharp
public static IReadOnlyList<string> Ids { get; }
```


Upstream's provider ids, in its order.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L76)

## Methods

<a id="E2E_OAuth_OAuthProviders_ApiKeyFor_"></a>
<a id="E2E_OAuth_OAuthProviders_ApiKeyFor_E2E_OAuth_IOAuthProvider_"></a>

### ApiKeyFor(IOAuthProvider)


```csharp
public static string? ApiKeyFor(IOAuthProvider provider)
```


The provider's API key from its [ApiKeyEnv](/api/E2E.OAuth/IOAuthProvider/#E2E_OAuth_IOAuthProvider_ApiKeyEnv) variable, when that is set.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `provider` | [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L108)

<a id="E2E_OAuth_OAuthProviders_CredentialSource_"></a>
<a id="E2E_OAuth_OAuthProviders_CredentialSource_E2E_OAuth_IOAuthProvider_E2E_OAuth_ICredentialStore_"></a>

### CredentialSource(IOAuthProvider, ICredentialStore?)


```csharp
public static (ICredentialStore Store, string LoginHint) CredentialSource(IOAuthProvider provider, ICredentialStore? store = null)
```


Where &lt;code class="paramref"&gt;provider&lt;/code&gt;'s calls read their credential: the API key, when set, as a login with nothing to
refresh, else &lt;code class="paramref"&gt;store&lt;/code&gt;, the default store unless given. The default store is only opened without a
key, so a malformed `E2E_OAUTH_CREDENTIALS` does not block the key. The hint names the remedy for the
credential actually read.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `provider` | [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |

**Returns:** `(ICredentialStore Store, string LoginHint)`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L120)

<a id="E2E_OAuth_OAuthProviders_Describe_"></a>
<a id="E2E_OAuth_OAuthProviders_Describe_System_String_"></a>

### Describe(string)


```csharp
public static string Describe(string id)
```


A short description of each subscription, for pickers and help.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L134)

<a id="E2E_OAuth_OAuthProviders_Get_"></a>
<a id="E2E_OAuth_OAuthProviders_Get_System_String_"></a>

### Get(string)


```csharp
public static IOAuthProvider Get(string id)
```


The provider for &lt;code class="paramref"&gt;id&lt;/code&gt;; [Misconfigured](/api/E2E.OAuth/OAuthException/#E2E_OAuth_OAuthException_Misconfigured) for an unknown id.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L82)

<a id="E2E_OAuth_OAuthProviders_ListModelsAsync_"></a>
<a id="E2E_OAuth_OAuthProviders_ListModelsAsync_System_String_E2E_OAuth_ICredentialStore_System_Threading_CancellationToken_"></a>

### ListModelsAsync(string, ICredentialStore?, CancellationToken)


```csharp
public static Task<IReadOnlyList<SubscriptionModel>> ListModelsAsync(string providerId, ICredentialStore? store = null, CancellationToken cancellationToken = default)
```


The models &lt;code class="paramref"&gt;providerId&lt;/code&gt;'s credential serves: its API key when set, as its model constructor reads it,
else the stored login; [NotLoggedIn](/api/E2E.OAuth/OAuthException/#E2E_OAuth_OAuthException_NotLoggedIn) without either.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SubscriptionModel&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L193)

<a id="E2E_OAuth_OAuthProviders_LoginAsync_"></a>
<a id="E2E_OAuth_OAuthProviders_LoginAsync_System_String_E2E_OAuth_OAuthLoginCallbacks_E2E_OAuth_OAuthLoginOptions_E2E_OAuth_ICredentialStore_System_Threading_CancellationToken_"></a>

### LoginAsync(string, OAuthLoginCallbacks, OAuthLoginOptions?, ICredentialStore?, CancellationToken)


```csharp
public static Task<OAuthCredentials> LoginAsync(string providerId, OAuthLoginCallbacks callbacks, OAuthLoginOptions? options = null, ICredentialStore? store = null, CancellationToken cancellationToken = default)
```


Signs in to &lt;code class="paramref"&gt;providerId&lt;/code&gt; and stores the credentials.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `callbacks` | [OAuthLoginCallbacks](/api/E2E.OAuth/OAuthLoginCallbacks/) |  |
| `options` | [OAuthLoginOptions](/api/E2E.OAuth/OAuthLoginOptions/) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L147)

<a id="E2E_OAuth_OAuthProviders_LoginHint_"></a>
<a id="E2E_OAuth_OAuthProviders_LoginHint_E2E_OAuth_IOAuthProvider_"></a>

### LoginHint(IOAuthProvider)


```csharp
public static string LoginHint(IOAuthProvider provider)
```


How a missing login is described to the user: the CLI command that fixes it, and the API key variable that does too.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `provider` | [IOAuthProvider](/api/E2E.OAuth/IOAuthProvider/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L100)

<a id="E2E_OAuth_OAuthProviders_LogoutAsync_"></a>
<a id="E2E_OAuth_OAuthProviders_LogoutAsync_System_String_E2E_OAuth_ICredentialStore_System_Threading_CancellationToken_"></a>

### LogoutAsync(string, ICredentialStore?, CancellationToken)


```csharp
public static Task<bool> LogoutAsync(string providerId, ICredentialStore? store = null, CancellationToken cancellationToken = default)
```


Forgets the stored login. True when one was stored.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `store` | [ICredentialStore](/api/E2E.OAuth/ICredentialStore/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/Subscriptions.cs#L176)
