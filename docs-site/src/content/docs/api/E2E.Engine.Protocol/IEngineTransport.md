---
title: "IEngineTransport"
description: "Opens connections to an engine server. Upstream's EngineTransport."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngineTransport"
  hidden: true
---

<a id="E2E_Engine_Protocol_IEngineTransport"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public interface IEngineTransport
```


Opens connections to an engine server. Upstream's `EngineTransport`.

**Derived:** [LoopbackTransport](/api/E2E.Engine.Protocol/LoopbackTransport/), [SocketTransport](/api/E2E.Engine.Protocol/SocketTransport/), [SpawnTransport](/api/E2E.Engine.Protocol/SpawnTransport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Properties

<a id="E2E_Engine_Protocol_IEngineTransport_Label_"></a>
<a id="E2E_Engine_Protocol_IEngineTransport_Label"></a>

### Label


```csharp
string Label { get; }
```


A short description for error messages: the command, or the address.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_Protocol_IEngineTransport_OpenAsync_"></a>
<a id="E2E_Engine_Protocol_IEngineTransport_OpenAsync_E2E_Engine_Protocol_TransportContext_"></a>

### OpenAsync(TransportContext)


```csharp
Task<ITransportConnection> OpenAsync(TransportContext context)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [TransportContext](/api/E2E.Engine.Protocol/TransportContext/) |  |

**Returns:** [Task&lt;ITransportConnection&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
