---
title: "SpawnTransport"
description: "Starts the server as a child process per connection and speaks over its stdin and stdout, the way an editor starts a language server. Its stderr is kept for..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SpawnTransport"
  hidden: true
---

<a id="E2E_Engine_Protocol_SpawnTransport"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed class SpawnTransport : IEngineTransport
```


Starts the server as a child process per connection and speaks over its stdin and stdout, the way an editor starts a language server. Its
stderr is kept for the message of a failure. Upstream's `spawnTransport`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEngineTransport](/api/E2E.Engine.Protocol/IEngineTransport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Constructors

<a id="E2E_Engine_Protocol_SpawnTransport__ctor_"></a>
<a id="E2E_Engine_Protocol_SpawnTransport__ctor_E2E_Engine_Protocol_SpawnTransportOptions_"></a>

### SpawnTransport(SpawnTransportOptions)


```csharp
public SpawnTransport(SpawnTransportOptions options)
```


Starts the server as a child process per connection and speaks over its stdin and stdout, the way an editor starts a language server. Its
stderr is kept for the message of a failure. Upstream's `spawnTransport`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [SpawnTransportOptions](/api/E2E.Engine.Protocol/SpawnTransportOptions/) |  |

## Properties

<a id="E2E_Engine_Protocol_SpawnTransport_Label_"></a>
<a id="E2E_Engine_Protocol_SpawnTransport_Label"></a>

### Label


```csharp
public string Label { get; }
```


A short description for error messages: the command, or the address.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_Protocol_SpawnTransport_OpenAsync_"></a>
<a id="E2E_Engine_Protocol_SpawnTransport_OpenAsync_E2E_Engine_Protocol_TransportContext_"></a>

### OpenAsync(TransportContext)


```csharp
public Task<ITransportConnection> OpenAsync(TransportContext context)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [TransportContext](/api/E2E.Engine.Protocol/TransportContext/) |  |

**Returns:** [Task&lt;ITransportConnection&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
