---
title: "RpcException"
description: "A failed response, thrown from RequestAsync. Upstream's RpcError."
editUrl: false
prev: false
next: false
sidebar:
  label: "RpcException"
  hidden: true
---

<a id="E2E_Engine_Protocol_RpcException"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed class RpcException : Exception, ISerializable
```


A failed response, thrown from `RequestAsync`. Upstream's `RpcError`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Connection.cs)

## Constructors

<a id="E2E_Engine_Protocol_RpcException__ctor_"></a>
<a id="E2E_Engine_Protocol_RpcException__ctor_E2E_Engine_Protocol_RpcErrorObject_"></a>

### RpcException(RpcErrorObject)


```csharp
public RpcException(RpcErrorObject error)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/) |  |

## Properties

<a id="E2E_Engine_Protocol_RpcException_Code_"></a>
<a id="E2E_Engine_Protocol_RpcException_Code"></a>

### Code


```csharp
public int Code { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcException_Data_"></a>
<a id="E2E_Engine_Protocol_RpcException_Data"></a>

### Data


```csharp
public JsonNode? Data { get; }
```


The error's `data`. .NET: the name `Data` would hide [Data](https://learn.microsoft.com/dotnet/api/system.exception.data).

**Value:** [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)
