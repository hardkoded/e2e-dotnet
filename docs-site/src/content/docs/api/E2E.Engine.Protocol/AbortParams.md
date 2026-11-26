---
title: "AbortParams"
description: "Params of $/abort. Upstream's AbortParams."
editUrl: false
prev: false
next: false
sidebar:
  label: "AbortParams"
  hidden: true
---

<a id="E2E_Engine_Protocol_AbortParams"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record AbortParams : IEquatable<AbortParams>
```


Params of `$/abort`. Upstream's `AbortParams`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;AbortParams&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_AbortParams__ctor_"></a>
<a id="E2E_Engine_Protocol_AbortParams__ctor_System_Int32_E2E_Engine_Protocol_RpcErrorObject_"></a>

### AbortParams(int, RpcErrorObject?)


```csharp
public AbortParams(int Id, RpcErrorObject? Reason = null)
```


Params of `$/abort`. Upstream's `AbortParams`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Reason` | [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/) |  |

## Properties

<a id="E2E_Engine_Protocol_AbortParams_Id_"></a>
<a id="E2E_Engine_Protocol_AbortParams_Id"></a>

### Id


```csharp
public int Id { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_AbortParams_Reason_"></a>
<a id="E2E_Engine_Protocol_AbortParams_Reason"></a>

### Reason


```csharp
public RpcErrorObject? Reason { get; init; }
```


**Value:** [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/)
