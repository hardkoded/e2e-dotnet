---
title: "Connection"
description: "The engine protocol's base layer, the Language Server Protocol's base protocol unchanged: JSON-RPC 2.0 messages, each framed by a Content-Length header, over..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Connection"
  hidden: true
---

<a id="E2E_Engine_Protocol_Connection"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed class Connection : IDisposable
```


The engine protocol's base layer, the Language Server Protocol's base protocol unchanged: JSON-RPC 2.0 messages, each framed by a
`Content-Length` header, over any byte stream. Either side may send requests and notifications; the harness is the client, the
engine the server. Nothing here knows an engine method. Requests are answered concurrently; messages are written in the order they are
sent, so a notification sent before a response is handled before it. Upstream's `Connection`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Connection.cs)

## Constructors

<a id="E2E_Engine_Protocol_Connection__ctor_"></a>
<a id="E2E_Engine_Protocol_Connection__ctor_E2E_Engine_Protocol_MessageStreams_E2E_Engine_Protocol_ConnectionOptions_"></a>

### Connection(MessageStreams, ConnectionOptions?)


```csharp
public Connection(MessageStreams streams, ConnectionOptions? options = null)
```


Reads and writes &lt;code class="paramref"&gt;streams&lt;/code&gt; until the input ends, fails, or the connection closes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `streams` | [MessageStreams](/api/E2E.Engine.Protocol/MessageStreams/) |  |
| `options` | [ConnectionOptions](/api/E2E.Engine.Protocol/ConnectionOptions/) |  |

## Properties

<a id="E2E_Engine_Protocol_Connection_Closed_"></a>
<a id="E2E_Engine_Protocol_Connection_Closed"></a>

### Closed


```csharp
public string? Closed { get; }
```


Why the connection closed, or null while it is open.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_Protocol_Connection_Close_"></a>
<a id="E2E_Engine_Protocol_Connection_Close_System_String_"></a>

### Close(string)


```csharp
public void Close(string reason)
```


Closes the connection: every pending request rejects with &lt;code class="paramref"&gt;reason&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_Engine_Protocol_Connection_Dispose_"></a>
<a id="E2E_Engine_Protocol_Connection_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Closes the connection and stops its readers. .NET-only: a stream is read by a task that must end.

<a id="E2E_Engine_Protocol_Connection_Notify_"></a>
<a id="E2E_Engine_Protocol_Connection_Notify_System_String_System_Text_Json_Nodes_JsonNode_"></a>

### Notify(string, JsonNode?)


```csharp
public void Notify(string method, JsonNode? parameters = null)
```


Sends a notification; dropped once the connection is closed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `method` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `parameters` | [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode) |  |

<a id="E2E_Engine_Protocol_Connection_OnRequest_"></a>
<a id="E2E_Engine_Protocol_Connection_OnRequest_System_String_System_Func_System_Text_Json_Nodes_JsonNode_System_Threading_Tasks_Task_System_Text_Json_Nodes_JsonNode___"></a>

### `OnRequest(string, Func<JsonNode?, Task<JsonNode?>>)`


```csharp
public void OnRequest(string method, Func<JsonNode?, Task<JsonNode?>> handler)
```


Registers a request handler after construction, for a method only known later.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `method` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `handler` | [Func&lt;JsonNode, Task&lt;JsonNode&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

<a id="E2E_Engine_Protocol_Connection_RequestAsync_"></a>
<a id="E2E_Engine_Protocol_Connection_RequestAsync_System_String_System_Text_Json_Nodes_JsonNode_"></a>

### RequestAsync(string, JsonNode?)


```csharp
public Task<JsonNode?> RequestAsync(string method, JsonNode? parameters = null)
```


Sends a request and resolves with its result; throws [RpcException](/api/E2E.Engine.Protocol/RpcException/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `method` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `parameters` | [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode) |  |

**Returns:** [Task&lt;JsonNode&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
