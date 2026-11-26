---
title: "LoopbackTransport"
description: "Serves an engine in this process over in-memory streams. Every call still crosses the protocol, serialized as on any other transport, so a suite run through..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LoopbackTransport"
  hidden: true
---

<a id="E2E_Engine_Protocol_LoopbackTransport"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed class LoopbackTransport : IEngineTransport
```


Serves an engine in this process over in-memory streams. Every call still crosses the protocol, serialized as on any other transport, so a
suite run through it proves an engine and the harness need nothing the protocol cannot carry. Upstream's `loopbackTransport`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEngineTransport](/api/E2E.Engine.Protocol/IEngineTransport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Constructors

<a id="E2E_Engine_Protocol_LoopbackTransport__ctor_"></a>
<a id="E2E_Engine_Protocol_LoopbackTransport__ctor_E2E_Engine_EngineHandle_"></a>

### LoopbackTransport(EngineHandle)


```csharp
public LoopbackTransport(EngineHandle engine)
```


Serves an engine in this process over in-memory streams. Every call still crosses the protocol, serialized as on any other transport, so a
suite run through it proves an engine and the harness need nothing the protocol cannot carry. Upstream's `loopbackTransport`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `engine` | [EngineHandle](/api/E2E.Engine/EngineHandle/) |  |

## Properties

<a id="E2E_Engine_Protocol_LoopbackTransport_Label_"></a>
<a id="E2E_Engine_Protocol_LoopbackTransport_Label"></a>

### Label


```csharp
public string Label { get; }
```


A short description for error messages: the command, or the address.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_Protocol_LoopbackTransport_OpenAsync_"></a>
<a id="E2E_Engine_Protocol_LoopbackTransport_OpenAsync_E2E_Engine_Protocol_TransportContext_"></a>

### OpenAsync(TransportContext)


```csharp
public Task<ITransportConnection> OpenAsync(TransportContext context)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [TransportContext](/api/E2E.Engine.Protocol/TransportContext/) |  |

**Returns:** [Task&lt;ITransportConnection&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
