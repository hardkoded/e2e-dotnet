---
title: "MessageStreams"
description: "A byte stream pair one connection speaks over. Upstream's MessageStreams."
editUrl: false
prev: false
next: false
sidebar:
  label: "MessageStreams"
  hidden: true
---

<a id="E2E_Engine_Protocol_MessageStreams"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record MessageStreams : IEquatable<MessageStreams>
```


A byte stream pair one connection speaks over. Upstream's `MessageStreams`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;MessageStreams&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Connection.cs)

## Constructors

<a id="E2E_Engine_Protocol_MessageStreams__ctor_"></a>
<a id="E2E_Engine_Protocol_MessageStreams__ctor_System_IO_Stream_System_IO_Stream_"></a>

### MessageStreams(Stream, Stream)


```csharp
public MessageStreams(Stream Input, Stream Output)
```


A byte stream pair one connection speaks over. Upstream's `MessageStreams`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Input` | [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream) |  |
| `Output` | [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream) |  |

## Properties

<a id="E2E_Engine_Protocol_MessageStreams_Input_"></a>
<a id="E2E_Engine_Protocol_MessageStreams_Input"></a>

### Input


```csharp
public Stream Input { get; init; }
```


**Value:** [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

<a id="E2E_Engine_Protocol_MessageStreams_Output_"></a>
<a id="E2E_Engine_Protocol_MessageStreams_Output"></a>

### Output


```csharp
public Stream Output { get; init; }
```


**Value:** [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)
