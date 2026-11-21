---
title: "TracingChannel"
description: "The five channels of a tracing channel (diagnosticsChannel.TracingChannel): start, end, asyncStart, asyncEnd and error. Each is a Channel named tracing:{name..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TracingChannel"
  hidden: true
---

<a id="E2E_Decision_Sdk_TracingChannel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class TracingChannel
```


The five channels of a tracing channel (`diagnosticsChannel.TracingChannel`): `start`, `end`, `asyncStart`,
`asyncEnd` and `error`. Each is a [Channel](/api/E2E.Decision.Sdk/Channel/) named `tracing:{name}:{event}`, as in Node.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DiagnosticsChannel.cs)

## Properties

<a id="E2E_Decision_Sdk_TracingChannel_AsyncEnd_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_AsyncEnd"></a>

### AsyncEnd


```csharp
public Channel AsyncEnd { get; }
```


Published when the operation's task completes.

**Value:** [Channel](/api/E2E.Decision.Sdk/Channel/)

<a id="E2E_Decision_Sdk_TracingChannel_AsyncStart_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_AsyncStart"></a>

### AsyncStart


```csharp
public Channel AsyncStart { get; }
```


Published when the operation's task completes, before [AsyncEnd](#E2E_Decision_Sdk_TracingChannel_AsyncEnd).

**Value:** [Channel](/api/E2E.Decision.Sdk/Channel/)

<a id="E2E_Decision_Sdk_TracingChannel_End_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_End"></a>

### End


```csharp
public Channel End { get; }
```


Published when the operation's synchronous part returns.

**Value:** [Channel](/api/E2E.Decision.Sdk/Channel/)

<a id="E2E_Decision_Sdk_TracingChannel_Error_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_Error"></a>

### Error


```csharp
public Channel Error { get; }
```


Published when the operation fails.

**Value:** [Channel](/api/E2E.Decision.Sdk/Channel/)

<a id="E2E_Decision_Sdk_TracingChannel_HasSubscribers_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_HasSubscribers"></a>

### HasSubscribers


```csharp
public bool HasSubscribers { get; }
```


`tracingChannel.hasSubscribers`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_TracingChannel_Start_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_Start"></a>

### Start


```csharp
public Channel Start { get; }
```


Published when the operation starts.

**Value:** [Channel](/api/E2E.Decision.Sdk/Channel/)

## Methods

<a id="E2E_Decision_Sdk_TracingChannel_Subscribe_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_Subscribe_E2E_Decision_Sdk_TracingChannelSubscribers_"></a>

### Subscribe(TracingChannelSubscribers)


```csharp
public void Subscribe(TracingChannelSubscribers subscribers)
```


`tracingChannel.subscribe`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `subscribers` | [TracingChannelSubscribers](/api/E2E.Decision.Sdk/TracingChannelSubscribers/) |  |

<a id="E2E_Decision_Sdk_TracingChannel_TracePromiseAsync_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_TracePromiseAsync__1_System_Func_System_Threading_Tasks_Task___0___E2E_Decision_Sdk_TelemetryTracingChannelMessage_"></a>

### `TracePromiseAsync<T>(Func<Task<T>>, TelemetryTracingChannelMessage)`


```csharp
public Task<T> TracePromiseAsync<T>(Func<Task<T>> execute, TelemetryTracingChannelMessage message)
```


`tracingChannel.tracePromise`: runs &lt;code class="paramref"&gt;execute&lt;/code&gt; and publishes `start` and `end` around the call, then
`error` (when it fails), `asyncStart` and `asyncEnd` when its task completes, with [Result](/api/E2E.Decision.Sdk/TelemetryTracingChannelMessage/#E2E_Decision_Sdk_TelemetryTracingChannelMessage_Result)
or [Error](/api/E2E.Decision.Sdk/TelemetryTracingChannelMessage/#E2E_Decision_Sdk_TelemetryTracingChannelMessage_Error) set on &lt;code class="paramref"&gt;message&lt;/code&gt;. The task's own result or failure is returned.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `execute` | [Func&lt;Task&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `message` | [TelemetryTracingChannelMessage](/api/E2E.Decision.Sdk/TelemetryTracingChannelMessage/) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_TracingChannel_Unsubscribe_"></a>
<a id="E2E_Decision_Sdk_TracingChannel_Unsubscribe_E2E_Decision_Sdk_TracingChannelSubscribers_"></a>

### Unsubscribe(TracingChannelSubscribers)


```csharp
public void Unsubscribe(TracingChannelSubscribers subscribers)
```


`tracingChannel.unsubscribe`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `subscribers` | [TracingChannelSubscribers](/api/E2E.Decision.Sdk/TracingChannelSubscribers/) |  |
