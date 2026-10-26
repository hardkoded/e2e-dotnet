---
title: "EnvironmentCredentialStore"
description: "Credentials from EnvironmentVariable: readable everywhere, changed nowhere."
editUrl: false
prev: false
next: false
sidebar:
  label: "EnvironmentCredentialStore"
  hidden: true
---

<a id="E2E_OAuth_EnvironmentCredentialStore"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class EnvironmentCredentialStore : ICredentialStore
```


Credentials from [EnvironmentVariable](/api/E2E.OAuth/CredentialStores/#E2E_OAuth_CredentialStores_EnvironmentVariable): readable everywhere, changed nowhere.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [ICredentialStore](/api/E2E.OAuth/ICredentialStore/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L231)

## Constructors

<a id="E2E_OAuth_EnvironmentCredentialStore__ctor_"></a>
<a id="E2E_OAuth_EnvironmentCredentialStore__ctor_System_String_"></a>

### EnvironmentCredentialStore(string)


```csharp
public EnvironmentCredentialStore(string json)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `json` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L235)

## Methods

<a id="E2E_OAuth_EnvironmentCredentialStore_GetAsync_"></a>
<a id="E2E_OAuth_EnvironmentCredentialStore_GetAsync_System_String_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L240)

<a id="E2E_OAuth_EnvironmentCredentialStore_ListAsync_"></a>
<a id="E2E_OAuth_EnvironmentCredentialStore_ListAsync_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L261)

<a id="E2E_OAuth_EnvironmentCredentialStore_RemoveAsync_"></a>
<a id="E2E_OAuth_EnvironmentCredentialStore_RemoveAsync_System_String_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L256)

<a id="E2E_OAuth_EnvironmentCredentialStore_SetAsync_"></a>
<a id="E2E_OAuth_EnvironmentCredentialStore_SetAsync_System_String_E2E_OAuth_OAuthCredentials_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L251)
