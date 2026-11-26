---
title: "SocketTransportOptions"
description: "Options of SocketTransport. Upstream's SocketTransportOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "SocketTransportOptions"
  hidden: true
---

<a id="E2E_Engine_Protocol_SocketTransportOptions"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record SocketTransportOptions : IEquatable<SocketTransportOptions>
```


Options of [SocketTransport](/api/E2E.Engine.Protocol/SocketTransport/). Upstream's `SocketTransportOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SocketTransportOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Constructors

<a id="E2E_Engine_Protocol_SocketTransportOptions__ctor_"></a>
<a id="E2E_Engine_Protocol_SocketTransportOptions__ctor_System_String_System_Int32_"></a>

### SocketTransportOptions(string, int)


```csharp
public SocketTransportOptions(string Host, int Port)
```


Options of [SocketTransport](/api/E2E.Engine.Protocol/SocketTransport/). Upstream's `SocketTransportOptions`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Host` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Port` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Engine_Protocol_SocketTransportOptions_Host_"></a>
<a id="E2E_Engine_Protocol_SocketTransportOptions_Host"></a>

### Host


```csharp
public string Host { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_SocketTransportOptions_Port_"></a>
<a id="E2E_Engine_Protocol_SocketTransportOptions_Port"></a>

### Port


```csharp
public int Port { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
