---
title: "E2E.Engine.Protocol"
description: "Public types in the `E2E.Engine.Protocol` namespace."
editUrl: false
sidebar:
  label: "E2E.Engine.Protocol"
---

[API reference](/api/)

Public types in the `E2E.Engine.Protocol` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [AbortParams](/api/E2E.Engine.Protocol/AbortParams/) | Params of $/abort. Upstream's AbortParams. |
| [ClientInfo](/api/E2E.Engine.Protocol/ClientInfo/) | Who the client is, for InitializeParams. |
| [ConnectEngineOptions](/api/E2E.Engine.Protocol/ConnectEngineOptions/) | Options of ConnectEngine(ConnectEngineOptions). Upstream's ConnectEngineOptions. |
| [Connection](/api/E2E.Engine.Protocol/Connection/) | The engine protocol's base layer, the Language Server Protocol's base protocol unchanged: JSON-RPC 2.0 messages, each framed by a Content-Length header, over any byte stream. Either side may send requests and notifications; the harness is the client, the engine the server. Nothing here knows an engine method. Requests are answered concurrently; messages are written in the order they are sent, so a notification sent before a response is handled before it. Upstream's Connection. |
| [ConnectionOptions](/api/E2E.Engine.Protocol/ConnectionOptions/) | How a connection handles what arrives on it. Upstream's ConnectionOptions. |
| [ConnectionRole](/api/E2E.Engine.Protocol/ConnectionRole/) | What a connection serves: a run's provisioning, a worker, or e2e doctor's checks. Upstream's ConnectionRole. |
| [DeriveSecretParams](/api/E2E.Engine.Protocol/DeriveSecretParams/) | Params of attempt/deriveSecret: the value the client resolved, for the forms the engine derives from it. Upstream's DeriveSecretParams. |
| [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/) | What an engine server is, as JSON: the static members of an engine and the methods it answers. The client declares it at config load, where the harness needs an engine's capabilities synchronously, and the server returns its own from initialize; the two must agree. Upstream's EngineManifest (engine/protocol/protocol.ts). |
| [InitializeParams](/api/E2E.Engine.Protocol/InitializeParams/) | Params of initialize. Upstream's InitializeParams. |
| [InitializeResult](/api/E2E.Engine.Protocol/InitializeResult/) | Result of initialize. Upstream's InitializeResult. |
| [LifecycleMethod](/api/E2E.Engine.Protocol/LifecycleMethod/) | A method that carries one lifecycle hook, whose single argument is an info or cleanup object. Upstream's LifecycleMethod. |
| [LoopbackTransport](/api/E2E.Engine.Protocol/LoopbackTransport/) | Serves an engine in this process over in-memory streams. Every call still crosses the protocol, serialized as on any other transport, so a suite run through it proves an engine and the harness need nothing the protocol cannot carry. Upstream's loopbackTransport. |
| [MessageStreams](/api/E2E.Engine.Protocol/MessageStreams/) | A byte stream pair one connection speaks over. Upstream's MessageStreams. |
| [OperationMethod](/api/E2E.Engine.Protocol/OperationMethod/) | A method that carries one engine operation: the member it calls and that member's arguments by name, in order. Upstream's OperationMethod. |
| [Protocol](/api/E2E.Engine.Protocol/Protocol/) | The engine protocol: the engine contract as messages, so an engine can run in another process, on another machine, or be written in another language. It follows the Language Server Protocol: the base layer is LSP's (Connection), a connection opens with initialize/initialized and ends with shutdown/exit, and the server announces what it answers in its manifest. Each engine member is one request whose params are the member's arguments as JSON. Upstream's engine/protocol/protocol.ts. |
| [ProtocolClient](/api/E2E.Engine.Protocol/ProtocolClient/) | The client half of the engine protocol: ConnectEngine(ConnectEngineOptions) turns an engine server into an ordinary engine handle, so the harness drives a server in another process, on another machine, or in another language exactly as it drives an engine in its own process. A handle opens three kinds of connection, mirroring where the harness calls an engine: a run connection per target from prepare to finish, a worker connection from init to dispose, and a doctor connection per target for e2e doctor's one call. None shares state with another. Upstream's engine/protocol/client.ts. |
| [ProtocolManifest](/api/E2E.Engine.Protocol/ProtocolManifest/) | The manifest: DescribeEngine derives one from an engine, CheckManifest checks the shape of a declared one, and ManifestMismatch compares the declared manifest with the one a server announces. Upstream's engine/protocol/manifest.ts. |
| [ProtocolMethod](/api/E2E.Engine.Protocol/ProtocolMethod/) | One entry of the methods table. Upstream's OperationMethod \| LifecycleMethod. |
| [ProtocolServer](/api/E2E.Engine.Protocol/ProtocolServer/) | The server half of the engine protocol: ServeEngineAsync answers the protocol with an engine built by DefineEngine(EngineSpec), so any engine written in .NET can run in a process of its own. An engine in another language implements the same messages; this file is their reference implementation. Upstream's engine/protocol/server.ts. |
| [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/) | The error object of a failed JSON-RPC response. Upstream's RpcErrorObject. |
| [RpcErrors](/api/E2E.Engine.Protocol/RpcErrors/) | The JSON-RPC error codes the protocol uses, LSP's numbering. Upstream's RPC_ERRORS (engine/protocol/connection.ts). |
| [RpcException](/api/E2E.Engine.Protocol/RpcException/) | A failed response, thrown from RequestAsync. Upstream's RpcError. |
| [SocketTransport](/api/E2E.Engine.Protocol/SocketTransport/) | Connects to a server already listening on a TCP port: a server on another machine, or one a launcher forwarded from a device. The server outlives the connection; closing ends the socket only. Upstream's socketTransport. |
| [SocketTransportOptions](/api/E2E.Engine.Protocol/SocketTransportOptions/) | Options of SocketTransport. Upstream's SocketTransportOptions. |
| [SpawnTransport](/api/E2E.Engine.Protocol/SpawnTransport/) | Starts the server as a child process per connection and speaks over its stdin and stdout, the way an editor starts a language server. Its stderr is kept for the message of a failure. Upstream's spawnTransport. |
| [SpawnTransportOptions](/api/E2E.Engine.Protocol/SpawnTransportOptions/) | Options of SpawnTransport. Upstream's SpawnTransportOptions. |
| [TransportContext](/api/E2E.Engine.Protocol/TransportContext/) | What a transport knows when it opens a connection. Upstream's TransportContext (engine/protocol/transport.ts). |
| [WireCleanup](/api/E2E.Engine.Protocol/WireCleanup/) | An engine cleanup context on the wire. Upstream's WireCleanup. |
| [WireFilePayload](/api/E2E.Engine.Protocol/WireFilePayload/) | One file of WireSetInputFilePayloads: Buffer is the file bytes as base64. |
| [WireOperation](/api/E2E.Engine.Protocol/WireOperation/) | An operation context on the wire. Upstream's WireOperation. |
| [WirePixels](/api/E2E.Engine.Protocol/WirePixels/) | Observation pixels on the wire: Data is base64. Upstream's WirePixels. |
| [WireSetInputFilePayloads](/api/E2E.Engine.Protocol/WireSetInputFilePayloads/) | Upstream's WireLocatorAction for setInputFilePayloads: file bytes, like snapshot pixels, cross as base64. |
| [WireSignal](/api/E2E.Engine.Protocol/WireSignal/) | An abort signal on the wire. Upstream's WireSignal: the client mints one id per signal and sends $/abort once it aborts. |
| [WireSnapshot](/api/E2E.Engine.Protocol/WireSnapshot/) | An engine snapshot on the wire: the snapshot with its pixels as base64. Upstream's WireSnapshot. |

## Interfaces

| Type | Summary |
| --- | --- |
| [IEngineTransport](/api/E2E.Engine.Protocol/IEngineTransport/) | Opens connections to an engine server. Upstream's EngineTransport. |
| [ITransportConnection](/api/E2E.Engine.Protocol/ITransportConnection/) | One open connection's streams. Upstream's TransportConnection. |
