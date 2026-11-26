---
title: "RpcErrors"
description: "The JSON-RPC error codes the protocol uses, LSP's numbering. Upstream's RPC_ERRORS (engine/protocol/connection.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "RpcErrors"
  hidden: true
---

<a id="E2E_Engine_Protocol_RpcErrors"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public static class RpcErrors
```


The JSON-RPC error codes the protocol uses, LSP's numbering. Upstream's `RPC_ERRORS` (`engine/protocol/connection.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Connection.cs)

## Fields

<a id="E2E_Engine_Protocol_RpcErrors_ConnectionClosed"></a>

### ConnectionClosed


```csharp
public const int ConnectionClosed = -32099
```


The connection closed before the request was answered.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrors_EngineError"></a>

### EngineError


```csharp
public const int EngineError = -32000
```


A server error carrying an e2e error in `data`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrors_InternalError"></a>

### InternalError


```csharp
public const int InternalError = -32603
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrors_InvalidParams"></a>

### InvalidParams


```csharp
public const int InvalidParams = -32602
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrors_InvalidRequest"></a>

### InvalidRequest


```csharp
public const int InvalidRequest = -32600
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrors_MethodNotFound"></a>

### MethodNotFound


```csharp
public const int MethodNotFound = -32601
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrors_ParseError"></a>

### ParseError


```csharp
public const int ParseError = -32700
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
