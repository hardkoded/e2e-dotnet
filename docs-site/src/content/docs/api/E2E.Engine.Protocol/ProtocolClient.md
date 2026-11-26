---
title: "ProtocolClient"
description: "The client half of the engine protocol: ConnectEngine(ConnectEngineOptions) turns an engine server into an ordinary engine handle, so the harness drives a se..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProtocolClient"
  hidden: true
---

<a id="E2E_Engine_Protocol_ProtocolClient"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public static class ProtocolClient
```


The client half of the engine protocol: [ConnectEngine(ConnectEngineOptions)](#E2E_Engine_Protocol_ProtocolClient_ConnectEngine_E2E_Engine_Protocol_ConnectEngineOptions_) turns an engine server into an ordinary engine handle, so the harness
drives a server in another process, on another machine, or in another language exactly as it drives an engine in its own process.
A handle opens three kinds of connection, mirroring where the harness calls an engine: a `run` connection per target from
`prepare` to `finish`, a `worker` connection from `init` to `dispose`, and a `doctor` connection per target
for `e2e doctor`'s one call. None shares state with another. Upstream's `engine/protocol/client.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

.NET-only holder name: C# has no free functions.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Engine_Protocol_ProtocolClient_ConnectEngine_"></a>
<a id="E2E_Engine_Protocol_ProtocolClient_ConnectEngine_E2E_Engine_Protocol_ConnectEngineOptions_"></a>

### ConnectEngine(ConnectEngineOptions)


```csharp
public static EngineHandle ConnectEngine(ConnectEngineOptions options)
```


Connects to an engine server and returns it as an engine handle, the value a target's engine takes. The handle is built from the
manifest and checked by [DefineEngine(EngineSpec)](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_), so a manifest is held to every rule an engine in this
process is. Upstream's `connectEngine`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ConnectEngineOptions](/api/E2E.Engine.Protocol/ConnectEngineOptions/) |  |

**Returns:** [EngineHandle](/api/E2E.Engine/EngineHandle/)

<a id="E2E_Engine_Protocol_ProtocolClient_ProtocolEngine_"></a>
<a id="E2E_Engine_Protocol_ProtocolClient_ProtocolEngine_E2E_Engine_EngineHandle_"></a>

### ProtocolEngine(EngineHandle)


```csharp
public static EngineHandle ProtocolEngine(EngineHandle engine)
```


The engine as the harness drives it: over the protocol. A [ConnectEngine(ConnectEngineOptions)](#E2E_Engine_Protocol_ProtocolClient_ConnectEngine_E2E_Engine_Protocol_ConnectEngineOptions_) handle already is; an engine in this process is
served over [LoopbackTransport](/api/E2E.Engine.Protocol/LoopbackTransport/), so every call crosses the wire format a remote engine would receive. Fixtures, the app
check, and the declared secrets stay the engine's own: they belong to the client half of any engine. One engine always gets the same
client, so a handle two targets or two config loads share stays one instance. Upstream's `protocolEngine`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `engine` | [EngineHandle](/api/E2E.Engine/EngineHandle/) |  |

**Returns:** [EngineHandle](/api/E2E.Engine/EngineHandle/)
