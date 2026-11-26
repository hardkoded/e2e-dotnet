---
title: "WireSignal"
description: "An abort signal on the wire. Upstream's WireSignal: the client mints one id per signal and sends $/abort once it aborts."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireSignal"
  hidden: true
---

<a id="E2E_Engine_Protocol_WireSignal"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WireSignal : IEquatable<WireSignal>
```


An abort signal on the wire. Upstream's `WireSignal`: the client mints one id per signal and sends `$/abort` once it aborts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WireSignal&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WireSignal__ctor_"></a>
<a id="E2E_Engine_Protocol_WireSignal__ctor_System_Int32_System_Nullable_System_Boolean__E2E_Engine_Protocol_RpcErrorObject_"></a>

### WireSignal(int, bool?, RpcErrorObject?)


```csharp
public WireSignal(int Id, bool? Aborted = null, RpcErrorObject? Reason = null)
```


An abort signal on the wire. Upstream's `WireSignal`: the client mints one id per signal and sends `$/abort` once it aborts.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Aborted` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Reason` | [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/) |  |

## Properties

<a id="E2E_Engine_Protocol_WireSignal_Aborted_"></a>
<a id="E2E_Engine_Protocol_WireSignal_Aborted"></a>

### Aborted


```csharp
public bool? Aborted { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_WireSignal_Id_"></a>
<a id="E2E_Engine_Protocol_WireSignal_Id"></a>

### Id


```csharp
public int Id { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_WireSignal_Reason_"></a>
<a id="E2E_Engine_Protocol_WireSignal_Reason"></a>

### Reason


```csharp
public RpcErrorObject? Reason { get; init; }
```


**Value:** [RpcErrorObject](/api/E2E.Engine.Protocol/RpcErrorObject/)
