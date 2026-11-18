---
title: "Host"
description: "One engine instance, initialized: the owner of the sessions opened on it, one at a time. Upstream's Host (core/launch.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Host"
  hidden: true
---

<a id="E2E_Host"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Host : IAsyncDisposable
```


One engine instance, initialized: the owner of the sessions opened on it, one at a time. Upstream's `Host` (`core/launch.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Properties

<a id="E2E_Host_Platform_"></a>
<a id="E2E_Host_Platform"></a>

### Platform


```csharp
public string Platform { get; }
```


The engine's platform label: `web`, `ios`, `android`, or an engine's own string.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Host_CloseAsync_"></a>
<a id="E2E_Host_CloseAsync"></a>

### CloseAsync()


```csharp
public Task CloseAsync()
```


Closes the open session, then the engine. Idempotent. Throws an [AggregateException](https://learn.microsoft.com/dotnet/api/system.aggregateexception) of the failures closing found. Upstream's `close`.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Host_DisposeAsync_"></a>
<a id="E2E_Host_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Closes the open session, then the engine. Idempotent. Throws an [AggregateException](https://learn.microsoft.com/dotnet/api/system.aggregateexception) of the failures closing found. Upstream's `close`.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

<a id="E2E_Host_NewSessionAsync_"></a>
<a id="E2E_Host_NewSessionAsync_E2E_NewSessionOptions_System_Threading_CancellationToken_"></a>

### NewSessionAsync(NewSessionOptions?, CancellationToken)


```csharp
public Task<Session> NewSessionAsync(NewSessionOptions? options = null, CancellationToken cancellationToken = default)
```


Opens a session. One at a time: the previous one must be closed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [NewSessionOptions](/api/E2E/NewSessionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Session&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Host_Secret_"></a>
<a id="E2E_Host_Secret_System_String_"></a>

### Secret(string)


```csharp
public Secret Secret(string name)
```


The handle of a secret passed to `LaunchAsync`, for `FillAsync` or an agent's params. An unknown name throws `SECRET_UNAVAILABLE`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Secret](/api/E2E/Secret/)
