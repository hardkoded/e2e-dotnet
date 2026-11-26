---
title: "InitializeParams"
description: "Params of initialize. Upstream's InitializeParams."
editUrl: false
prev: false
next: false
sidebar:
  label: "InitializeParams"
  hidden: true
---

<a id="E2E_Engine_Protocol_InitializeParams"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record InitializeParams : IEquatable<InitializeParams>
```


Params of `initialize`. Upstream's `InitializeParams`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;InitializeParams&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_InitializeParams__ctor_"></a>
<a id="E2E_Engine_Protocol_InitializeParams__ctor_System_Int32_E2E_Engine_Protocol_ClientInfo_System_String_System_Text_Json_Nodes_JsonNode_"></a>

### InitializeParams(int, ClientInfo, string, JsonNode?)


```csharp
public InitializeParams(int ProtocolVersion, ClientInfo ClientInfo, string Role, JsonNode? InitializationOptions = null)
```


Params of `initialize`. Upstream's `InitializeParams`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ProtocolVersion` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `ClientInfo` | [ClientInfo](/api/E2E.Engine.Protocol/ClientInfo/) |  |
| `Role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `InitializationOptions` | [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode) |  |

## Properties

<a id="E2E_Engine_Protocol_InitializeParams_ClientInfo_"></a>
<a id="E2E_Engine_Protocol_InitializeParams_ClientInfo"></a>

### ClientInfo


```csharp
public ClientInfo ClientInfo { get; init; }
```


**Value:** [ClientInfo](/api/E2E.Engine.Protocol/ClientInfo/)

<a id="E2E_Engine_Protocol_InitializeParams_InitializationOptions_"></a>
<a id="E2E_Engine_Protocol_InitializeParams_InitializationOptions"></a>

### InitializationOptions


```csharp
public JsonNode? InitializationOptions { get; init; }
```


**Value:** [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

<a id="E2E_Engine_Protocol_InitializeParams_ProtocolVersion_"></a>
<a id="E2E_Engine_Protocol_InitializeParams_ProtocolVersion"></a>

### ProtocolVersion


```csharp
public int ProtocolVersion { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_InitializeParams_Role_"></a>
<a id="E2E_Engine_Protocol_InitializeParams_Role"></a>

### Role


```csharp
public string Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
