---
title: "Protocol"
description: "The engine protocol: the engine contract as messages, so an engine can run in another process, on another machine, or be written in another language. It foll..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Protocol"
  hidden: true
---

<a id="E2E_Engine_Protocol_Protocol"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public static class Protocol
```


The engine protocol: the engine contract as messages, so an engine can run in another process, on another machine, or be written in
another language. It follows the Language Server Protocol: the base layer is LSP's ([Connection](/api/E2E.Engine.Protocol/Connection/)), a connection opens
with `initialize`/`initialized` and ends with `shutdown`/`exit`, and the server announces what it answers in its
manifest. Each engine member is one request whose params are the member's arguments as JSON. Upstream's `engine/protocol/protocol.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

.NET-only holder name: C# has no free functions.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Fields

<a id="E2E_Engine_Protocol_Protocol_EngineProtocolVersion"></a>

### EngineProtocolVersion


```csharp
public const int EngineProtocolVersion = 1
```


The protocol version this runner speaks; `initialize` refuses any other. Upstream's `ENGINE_PROTOCOL_VERSION`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_Protocol_RemoteStackKey"></a>

### RemoteStackKey


```csharp
public const string RemoteStackKey = "e2e.remoteStack"
```


The [Data](https://learn.microsoft.com/dotnet/api/system.exception.data) key a decoded error keeps the stack it crossed with under. .NET-only: a .NET stack trace is read-only.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

<a id="E2E_Engine_Protocol_Protocol_EngineMethods_"></a>
<a id="E2E_Engine_Protocol_Protocol_EngineMethods"></a>

### EngineMethods


```csharp
public static IReadOnlyList<string> EngineMethods { get; }
```


Every engine method a server may answer, in protocol order. Upstream's `ENGINE_METHODS`.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_Protocol_Methods_"></a>
<a id="E2E_Engine_Protocol_Protocol_Methods"></a>

### Methods


```csharp
public static IReadOnlyDictionary<string, ProtocolMethod> Methods { get; }
```


Every engine method a server may answer, one per engine member: the one table the manifest, the client, and the server are all read
off. Upstream's `METHODS`, in protocol order.

**Value:** [IReadOnlyDictionary&lt;string, ProtocolMethod&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_Protocol_Protocol_ServedMethods_"></a>
<a id="E2E_Engine_Protocol_Protocol_ServedMethods"></a>

### ServedMethods


```csharp
public static IReadOnlySet<string> ServedMethods { get; }
```


Methods `serveEngine` answers for any engine: it tracks the attempt whether or not the engine has hooks for it. Upstream's `SERVED_METHODS`.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

## Methods

<a id="E2E_Engine_Protocol_Protocol_DecodeError_"></a>
<a id="E2E_Engine_Protocol_Protocol_DecodeError_E2E_Engine_Protocol_RpcErrorObject_System_Int32_"></a>

### DecodeError(RpcErrorObject, int)


```csharp
public static Exception DecodeError(RpcErrorObject error, int depth = 0)
```


Decodes an error response into the error the engine member threw, with the causes it crossed with. Upstream's `decodeError`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/) |  |
| `depth` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

<a id="E2E_Engine_Protocol_Protocol_DecodeLocatorAction_"></a>
<a id="E2E_Engine_Protocol_Protocol_DecodeLocatorAction_System_Object_"></a>

### DecodeLocatorAction(object)


```csharp
public static LocatorAction DecodeLocatorAction(object action)
```


Restores file payload bytes at the engine protocol boundary. Upstream's `decodeLocatorAction`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `action` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

**Returns:** [LocatorAction](/api/E2E.Engine/LocatorAction/)

<a id="E2E_Engine_Protocol_Protocol_DecodeSnapshot_"></a>
<a id="E2E_Engine_Protocol_Protocol_DecodeSnapshot_E2E_Engine_Protocol_WireSnapshot_"></a>

### DecodeSnapshot(WireSnapshot)


```csharp
public static EngineSnapshot DecodeSnapshot(WireSnapshot wire)
```


Decodes a snapshot off the wire. Upstream's `decodeSnapshot`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `wire` | [WireSnapshot](/api/E2E.Engine.Protocol/WireSnapshot/) |  |

**Returns:** [EngineSnapshot](/api/E2E.Engine/EngineSnapshot/)

<a id="E2E_Engine_Protocol_Protocol_EncodeAbortReason_"></a>
<a id="E2E_Engine_Protocol_Protocol_EncodeAbortReason_System_Exception_"></a>

### EncodeAbortReason(Exception?)


```csharp
public static RpcErrorObject? EncodeAbortReason(Exception? reason)
```


Encodes an abort reason, or null for the platform's own cancellation, which the receiving side reproduces by aborting without a
reason. Upstream's `encodeAbortReason`, whose platform error is `AbortError`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reason` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

**Returns:** [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/)

<a id="E2E_Engine_Protocol_Protocol_EncodeError_"></a>
<a id="E2E_Engine_Protocol_Protocol_EncodeError_System_Exception_System_Int32_"></a>

### EncodeError(Exception, int)


```csharp
public static RpcErrorObject EncodeError(Exception cause, int depth = 0)
```


Encodes what an engine member threw. An [EngineException](/api/E2E/EngineException/) and a harness error keep their code and category, so the
harness classifies a remote failure exactly as a local one; anything else crosses as its message, which the harness treats as an
infrastructure failure, as it does locally. Every error keeps its stack and its first causes. Upstream's `encodeError`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `depth` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/)

<a id="E2E_Engine_Protocol_Protocol_EncodeLocatorAction_"></a>
<a id="E2E_Engine_Protocol_Protocol_EncodeLocatorAction_E2E_Engine_LocatorAction_"></a>

### EncodeLocatorAction(LocatorAction)


```csharp
public static object EncodeLocatorAction(LocatorAction action)
```


Encodes file payload bytes without changing other locator actions. Returns a [WireSetInputFilePayloads](/api/E2E.Engine.Protocol/WireSetInputFilePayloads/) for
[LocatorAction.SetInputFilePayloads](/api/E2E.Engine/LocatorAction.SetInputFilePayloads/) and the same action instance for any other kind: upstream's
`WireLocatorAction` is a union of the two.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `action` | [LocatorAction](/api/E2E.Engine/LocatorAction/) |  |

**Returns:** [object](https://learn.microsoft.com/dotnet/api/system.object)

<a id="E2E_Engine_Protocol_Protocol_EncodeSnapshot_"></a>
<a id="E2E_Engine_Protocol_Protocol_EncodeSnapshot_E2E_Engine_EngineSnapshot_"></a>

### EncodeSnapshot(EngineSnapshot)


```csharp
public static WireSnapshot EncodeSnapshot(EngineSnapshot snapshot)
```


Encodes a snapshot for the wire. Upstream's `encodeSnapshot`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `snapshot` | [EngineSnapshot](/api/E2E.Engine/EngineSnapshot/) |  |

**Returns:** [WireSnapshot](/api/E2E.Engine.Protocol/WireSnapshot/)

<a id="E2E_Engine_Protocol_Protocol_EngineMember_"></a>
<a id="E2E_Engine_Protocol_Protocol_EngineMember_E2E_Engine_EngineHandle_System_String_"></a>

### EngineMember(EngineHandle, string)


```csharp
public static Delegate? EngineMember(EngineHandle engine, string member)
```


An engine member's function, read off a `member` path of [Methods](#E2E_Engine_Protocol_Protocol_Methods); null when the engine lacks it. Upstream's
`engineMember`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `engine` | [EngineHandle](/api/E2E.Engine/EngineHandle/) |  |
| `member` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Delegate](https://learn.microsoft.com/dotnet/api/system.delegate)
