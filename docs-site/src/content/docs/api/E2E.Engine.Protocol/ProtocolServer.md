---
title: "ProtocolServer"
description: "The server half of the engine protocol: ServeEngineAsync answers the protocol with an engine built by DefineEngine(EngineSpec), so any engine written in .NET..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProtocolServer"
  hidden: true
---

<a id="E2E_Engine_Protocol_ProtocolServer"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public static class ProtocolServer
```


The server half of the engine protocol: `ServeEngineAsync` answers the protocol with an engine
built by [DefineEngine(EngineSpec)](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_), so any engine written in .NET can run in a process of its own. An engine in another
language implements the same messages; this file is their reference implementation. Upstream's `engine/protocol/server.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

.NET-only holder name: C# has no free functions.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Server.cs)

## Methods

<a id="E2E_Engine_Protocol_ProtocolServer_ServeEngineAsync_"></a>
<a id="E2E_Engine_Protocol_ProtocolServer_ServeEngineAsync_E2E_Engine_EngineHandle_E2E_Engine_Protocol_MessageStreams_"></a>

### ServeEngineAsync(EngineHandle, MessageStreams?)


```csharp
public static Task ServeEngineAsync(EngineHandle engine, MessageStreams? streams = null)
```


Serves &lt;code class="paramref"&gt;engine&lt;/code&gt; over one connection until the client sends `exit` or the streams close. Completes once the
connection is closed; a server process then exits. Defaults to the process's stdin and stdout, the transport a client's
[SpawnTransport](/api/E2E.Engine.Protocol/SpawnTransport/) opens: keep the engine's own logging on stderr. Upstream's `serveEngine`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `engine` | [EngineHandle](/api/E2E.Engine/EngineHandle/) |  |
| `streams` | [MessageStreams](/api/E2E.Engine.Protocol/MessageStreams/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_Protocol_ProtocolServer_ServeEngineAsync_System_Func_System_Text_Json_Nodes_JsonNode_System_Threading_Tasks_Task_E2E_Engine_EngineHandle___E2E_Engine_Protocol_MessageStreams_"></a>

### `ServeEngineAsync(Func<JsonNode?, Task<EngineHandle>>, MessageStreams?)`


```csharp
public static Task ServeEngineAsync(Func<JsonNode?, Task<EngineHandle>> factory, MessageStreams? streams = null)
```


`ServeEngineAsync` with a factory that builds the engine from the client's
`initializationOptions`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `factory` | [Func&lt;JsonNode, Task&lt;EngineHandle&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `streams` | [MessageStreams](/api/E2E.Engine.Protocol/MessageStreams/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
