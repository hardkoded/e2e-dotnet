---
title: "E2EHost"
description: "The worker-scoped owner of preparation, app processes, and engine initialization, that opens one isolated session at a time. Upstream's E2EHost (run/open-ses..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2EHost"
  hidden: true
---

<a id="E2E_E2EHost"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class E2EHost : IAsyncDisposable
```


The worker-scoped owner of preparation, app processes, and engine initialization, that opens one isolated session at a time. Upstream's
`E2EHost` (`run/open-session.ts`). Open one with `OpenAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L72)

## Properties

<a id="E2E_E2EHost_RunId_"></a>
<a id="E2E_E2EHost_RunId"></a>

### RunId


```csharp
public string RunId { get; }
```


The id of the run the host belongs to; the same as its sessions'. Upstream's `runId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L96)

<a id="E2E_E2EHost_Target_"></a>
<a id="E2E_E2EHost_Target"></a>

### Target


```csharp
public string Target { get; }
```


The name of the target the host opened. Upstream's `target`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L99)

## Methods

<a id="E2E_E2EHost_CloseAsync_"></a>
<a id="E2E_E2EHost_CloseAsync"></a>

### CloseAsync()


```csharp
public Task<IReadOnlyList<Exception>> CloseAsync()
```


Cancels or ends a pending or active session, then disposes the worker and releases resources, once. A second call returns what the first did.
Upstream's `close`, which returns `SerializedError[]`: see [CloseAsync()](/api/E2E/E2ESession/#E2E_E2ESession_CloseAsync).

**Returns:** [Task&lt;IReadOnlyList&lt;Exception&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L178)

<a id="E2E_E2EHost_DisposeAsync_"></a>
<a id="E2E_E2EHost_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Closes the host and throws what [CloseAsync()](#E2E_E2EHost_CloseAsync) returns as an [AggregateException](https://learn.microsoft.com/dotnet/api/system.aggregateexception).

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L181)

<a id="E2E_E2EHost_OpenAsync_"></a>
<a id="E2E_E2EHost_OpenAsync_E2E_OpenHostOptions_System_Threading_CancellationToken_"></a>

### OpenAsync(OpenHostOptions?, CancellationToken)


```csharp
public static Task<E2EHost> OpenAsync(OpenHostOptions? options = null, CancellationToken cancellationToken = default)
```


Opens the worker-scoped owner of preparation, app processes, and engine initialization. A cancelled &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; cancels
provisioning, initialization, and every session's operations; close still releases resources. Upstream's `openHost`, which is a
free function: C# has none, so the type it returns owns it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [OpenHostOptions](/api/E2E/OpenHostOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;E2EHost&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L106)

<a id="E2E_E2EHost_OpenSessionAsync_"></a>
<a id="E2E_E2EHost_OpenSessionAsync_E2E_OpenSessionOptions_System_Threading_CancellationToken_"></a>

### OpenSessionAsync(OpenSessionOptions, CancellationToken)


```csharp
public Task<E2ESession> OpenSessionAsync(OpenSessionOptions options, CancellationToken cancellationToken = default)
```


Opens one isolated session. One opening, live, or closing session per host; open them one after another. Upstream's `openSession`.
The token cancels the opening and, as the session's own, every operation it runs.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [OpenSessionOptions](/api/E2E/OpenSessionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;E2ESession&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L155)
