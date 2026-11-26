---
title: "RpcErrorObject"
description: "The error object of a failed JSON-RPC response. Upstream's RpcErrorObject."
editUrl: false
prev: false
next: false
sidebar:
  label: "RpcErrorObject"
  hidden: true
---

<a id="E2E_Engine_Protocol_RpcErrorObject"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record RpcErrorObject : IEquatable<RpcErrorObject>
```


The error object of a failed JSON-RPC response. Upstream's `RpcErrorObject`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RpcErrorObject&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Connection.cs)

## Constructors

<a id="E2E_Engine_Protocol_RpcErrorObject__ctor_"></a>
<a id="E2E_Engine_Protocol_RpcErrorObject__ctor_System_Int32_System_String_System_Text_Json_Nodes_JsonNode_"></a>

### RpcErrorObject(int, string, JsonNode?)


```csharp
public RpcErrorObject(int Code, string Message, JsonNode? Data = null)
```


The error object of a failed JSON-RPC response. Upstream's `RpcErrorObject`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Code` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Data` | [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode) |  |

## Properties

<a id="E2E_Engine_Protocol_RpcErrorObject_Code_"></a>
<a id="E2E_Engine_Protocol_RpcErrorObject_Code"></a>

### Code


```csharp
public int Code { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_RpcErrorObject_Data_"></a>
<a id="E2E_Engine_Protocol_RpcErrorObject_Data"></a>

### Data


```csharp
public JsonNode? Data { get; init; }
```


**Value:** [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

<a id="E2E_Engine_Protocol_RpcErrorObject_Message_"></a>
<a id="E2E_Engine_Protocol_RpcErrorObject_Message"></a>

### Message


```csharp
public string Message { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_Protocol_RpcErrorObject_IsRpcErrorObject_"></a>
<a id="E2E_Engine_Protocol_RpcErrorObject_IsRpcErrorObject_System_Text_Json_Nodes_JsonNode_E2E_Engine_Protocol_RpcErrorObject__"></a>

### IsRpcErrorObject(JsonNode?, out RpcErrorObject)


```csharp
public static bool IsRpcErrorObject(JsonNode? value, out RpcErrorObject error)
```


Whether &lt;code class="paramref"&gt;value&lt;/code&gt; has the shape of a JSON-RPC error object, and reads it. Upstream's `isRpcErrorObject`, which
narrows the value where C# returns what it read.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode) |  |
| `error` | [RpcErrorObject](./) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
