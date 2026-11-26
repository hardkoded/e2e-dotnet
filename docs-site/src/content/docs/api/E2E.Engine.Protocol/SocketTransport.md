---
title: "SocketTransport"
description: "Connects to a server already listening on a TCP port: a server on another machine, or one a launcher forwarded from a device. The server outlives the connect..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SocketTransport"
  hidden: true
---

<a id="E2E_Engine_Protocol_SocketTransport"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed class SocketTransport : IEngineTransport
```


Connects to a server already listening on a TCP port: a server on another machine, or one a launcher forwarded from a device. The server
outlives the connection; closing ends the socket only. Upstream's `socketTransport`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEngineTransport](/api/E2E.Engine.Protocol/IEngineTransport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Constructors

<a id="E2E_Engine_Protocol_SocketTransport__ctor_"></a>
<a id="E2E_Engine_Protocol_SocketTransport__ctor_E2E_Engine_Protocol_SocketTransportOptions_"></a>

### SocketTransport(SocketTransportOptions)


```csharp
public SocketTransport(SocketTransportOptions options)
```


Connects to a server already listening on a TCP port: a server on another machine, or one a launcher forwarded from a device. The server
outlives the connection; closing ends the socket only. Upstream's `socketTransport`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [SocketTransportOptions](/api/E2E.Engine.Protocol/SocketTransportOptions/) |  |

## Properties

<a id="E2E_Engine_Protocol_SocketTransport_Label_"></a>
<a id="E2E_Engine_Protocol_SocketTransport_Label"></a>

### Label


```csharp
public string Label { get; }
```


A short description for error messages: the command, or the address.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_Protocol_SocketTransport_OpenAsync_"></a>
<a id="E2E_Engine_Protocol_SocketTransport_OpenAsync_E2E_Engine_Protocol_TransportContext_"></a>

### OpenAsync(TransportContext)


```csharp
public Task<ITransportConnection> OpenAsync(TransportContext context)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [TransportContext](/api/E2E.Engine.Protocol/TransportContext/) |  |

**Returns:** [Task&lt;ITransportConnection&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
