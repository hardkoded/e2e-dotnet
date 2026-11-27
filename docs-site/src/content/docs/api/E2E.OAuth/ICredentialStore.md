---
title: "ICredentialStore"
description: "Where credentials live between runs, keyed by provider id."
editUrl: false
prev: false
next: false
sidebar:
  label: "ICredentialStore"
  hidden: true
---

<a id="E2E_OAuth_ICredentialStore"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public interface ICredentialStore
```


Where credentials live between runs, keyed by provider id.

**Derived:** [EnvironmentCredentialStore](/api/E2E.OAuth/EnvironmentCredentialStore/), [FileCredentialStore](/api/E2E.OAuth/FileCredentialStore/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs)

## Methods

<a id="E2E_OAuth_ICredentialStore_ExclusiveAsync_"></a>
<a id="E2E_OAuth_ICredentialStore_ExclusiveAsync__1_System_String_System_Func_System_Threading_Tasks_Task___0___System_Threading_CancellationToken_"></a>

### `ExclusiveAsync<T>(string, Func<Task<T>>, CancellationToken)`


```csharp
Task<T> ExclusiveAsync<T>(string providerId, Func<Task<T>> refresh, CancellationToken cancellationToken = default)
```


Runs &lt;code class="paramref"&gt;refresh&lt;/code&gt; while no other process refreshes &lt;code class="paramref"&gt;providerId&lt;/code&gt;'s login from this store, so a
rotating refresh token is sent once however many workers need it.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `refresh` | [Func&lt;Task&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_ICredentialStore_GetAsync_"></a>
<a id="E2E_OAuth_ICredentialStore_GetAsync_System_String_System_Threading_CancellationToken_"></a>

### GetAsync(string, CancellationToken)


```csharp
Task<OAuthCredentials?> GetAsync(string providerId, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_ICredentialStore_ListAsync_"></a>
<a id="E2E_OAuth_ICredentialStore_ListAsync_System_Threading_CancellationToken_"></a>

### ListAsync(CancellationToken)


```csharp
Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
```


Every provider id with stored credentials.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_ICredentialStore_RemoveAsync_"></a>
<a id="E2E_OAuth_ICredentialStore_RemoveAsync_System_String_System_Threading_CancellationToken_"></a>

### RemoveAsync(string, CancellationToken)


```csharp
Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default)
```


Forgets the login. True when one was stored.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_ICredentialStore_ReplaceAsync_"></a>
<a id="E2E_OAuth_ICredentialStore_ReplaceAsync_System_String_E2E_OAuth_OAuthCredentials_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### ReplaceAsync(string, OAuthCredentials, OAuthCredentials, CancellationToken)


```csharp
Task<bool> ReplaceAsync(string providerId, OAuthCredentials expected, OAuthCredentials next, CancellationToken cancellationToken = default)
```


Stores &lt;code class="paramref"&gt;next&lt;/code&gt; only while the stored login is still &lt;code class="paramref"&gt;expected&lt;/code&gt;; false when a
login or logout replaced it meanwhile, which then stands.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `next` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_ICredentialStore_SetAsync_"></a>
<a id="E2E_OAuth_ICredentialStore_SetAsync_System_String_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### SetAsync(string, OAuthCredentials, CancellationToken)


```csharp
Task SetAsync(string providerId, OAuthCredentials credentials, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
