---
title: "FileCredentialStore"
description: "One JSON file keyed by provider id, mode 0600. Writes hold an advisory lock and replace the file atomically, so two runs refreshing at once neither tear the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "FileCredentialStore"
  hidden: true
---

<a id="E2E_OAuth_FileCredentialStore"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class FileCredentialStore : ICredentialStore
```


One JSON file keyed by provider id, mode 0600. Writes hold an advisory lock and replace the file atomically,
so two runs refreshing at once neither tear the file nor lose each other's entries. Entries this store does
not understand are kept as they are.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [ICredentialStore](/api/E2E.OAuth/ICredentialStore/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs)

## Constructors

<a id="E2E_OAuth_FileCredentialStore__ctor_"></a>
<a id="E2E_OAuth_FileCredentialStore__ctor_System_String_"></a>

### FileCredentialStore(string)


```csharp
public FileCredentialStore(string path)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_OAuth_FileCredentialStore_FilePath_"></a>
<a id="E2E_OAuth_FileCredentialStore_FilePath"></a>

### FilePath


```csharp
public string FilePath { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_OAuth_FileCredentialStore_ExclusiveAsync_"></a>
<a id="E2E_OAuth_FileCredentialStore_ExclusiveAsync__1_System_String_System_Func_System_Threading_Tasks_Task___0___System_Threading_CancellationToken_"></a>

### `ExclusiveAsync<T>(string, Func<Task<T>>, CancellationToken)`


```csharp
public Task<T> ExclusiveAsync<T>(string providerId, Func<Task<T>> refresh, CancellationToken cancellationToken = default)
```


Runs &lt;code class="paramref"&gt;refresh&lt;/code&gt; holding this login's lock file, so worker processes refresh it one at a time.

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

<a id="E2E_OAuth_FileCredentialStore_GetAsync_"></a>
<a id="E2E_OAuth_FileCredentialStore_GetAsync_System_String_System_Threading_CancellationToken_"></a>

### GetAsync(string, CancellationToken)


```csharp
public Task<OAuthCredentials?> GetAsync(string providerId, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;OAuthCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_FileCredentialStore_ListAsync_"></a>
<a id="E2E_OAuth_FileCredentialStore_ListAsync_System_Threading_CancellationToken_"></a>

### ListAsync(CancellationToken)


```csharp
public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
```


Every provider id with stored credentials.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_FileCredentialStore_RemoveAsync_"></a>
<a id="E2E_OAuth_FileCredentialStore_RemoveAsync_System_String_System_Threading_CancellationToken_"></a>

### RemoveAsync(string, CancellationToken)


```csharp
public Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default)
```


Forgets the login. True when one was stored.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_OAuth_FileCredentialStore_ReplaceAsync_"></a>
<a id="E2E_OAuth_FileCredentialStore_ReplaceAsync_System_String_E2E_OAuth_OAuthCredentials_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### ReplaceAsync(string, OAuthCredentials, OAuthCredentials, CancellationToken)


```csharp
public Task<bool> ReplaceAsync(string providerId, OAuthCredentials expected, OAuthCredentials next, CancellationToken cancellationToken = default)
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

<a id="E2E_OAuth_FileCredentialStore_SetAsync_"></a>
<a id="E2E_OAuth_FileCredentialStore_SetAsync_System_String_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

### SetAsync(string, OAuthCredentials, CancellationToken)


```csharp
public Task SetAsync(string providerId, OAuthCredentials credentials, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `credentials` | [OAuthCredentials](/api/E2E.OAuth/OAuthCredentials/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
