---
title: "ITransportConnection"
description: "One open connection's streams. Upstream's TransportConnection."
editUrl: false
prev: false
next: false
sidebar:
  label: "ITransportConnection"
  hidden: true
---

<a id="E2E_Engine_Protocol_ITransportConnection"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public interface ITransportConnection
```


One open connection's streams. Upstream's `TransportConnection`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Properties

<a id="E2E_Engine_Protocol_ITransportConnection_Input_"></a>
<a id="E2E_Engine_Protocol_ITransportConnection_Input"></a>

### Input


```csharp
Stream Input { get; }
```


The stream the server's messages arrive on.

**Value:** [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

<a id="E2E_Engine_Protocol_ITransportConnection_Output_"></a>
<a id="E2E_Engine_Protocol_ITransportConnection_Output"></a>

### Output


```csharp
Stream Output { get; }
```


The stream the client's messages go out on.

**Value:** [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

## Methods

<a id="E2E_Engine_Protocol_ITransportConnection_CloseAsync_"></a>
<a id="E2E_Engine_Protocol_ITransportConnection_CloseAsync_System_TimeSpan_"></a>

### CloseAsync(TimeSpan)


```csharp
Task CloseAsync(TimeSpan timeout)
```


Releases what the transport holds (a process, a socket), within &lt;code class="paramref"&gt;timeout&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_Protocol_ITransportConnection_Diagnostics_"></a>
<a id="E2E_Engine_Protocol_ITransportConnection_Diagnostics"></a>

### Diagnostics()


```csharp
string Diagnostics()
```


What the transport saw go wrong (a server's last stderr lines), for an error message. Upstream's optional `diagnostics`.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
