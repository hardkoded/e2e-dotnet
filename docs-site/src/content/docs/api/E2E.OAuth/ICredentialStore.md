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

<a id="E2E_OAuth_ICredentialStore_ExclusiveAsync__1_"></a>
<a id="E2E_OAuth_ICredentialStore_ExclusiveAsync__1_System_String_System_Func_System_Threading_Tasks_Task{``0}__System_Threading_CancellationToken_"></a>

### ExclusiveAsync&lt;T&gt;(string, Func&lt;Task&lt;T&gt;&gt;, CancellationToken)


```csharp
Task<T> ExclusiveAsync<T>(string providerId, Func<Task<T>> refresh, CancellationToken cancellationToken = default)
```


Runs <code>refresh</code> while no other process refreshes <code>providerId</code>'s login from this store, so a rotating refresh token is sent once however many workers need it.

**Type Parameters**

`T` 

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `refresh` | [Func](https://learn.microsoft.com/dotnet/api/system.func-1)&lt;[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)&lt;T&gt;&gt; |  |
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
