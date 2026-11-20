---
title: "TelemetryTracingChannelMessage"
description: "TelemetryTracingChannelMessage: what a tracing channel publishes. Result and Error are set, as Node does on the message, before asyncEnd (and error) are publ..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TelemetryTracingChannelMessage"
  hidden: true
---

<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class TelemetryTracingChannelMessage
```


`TelemetryTracingChannelMessage`: what a tracing channel publishes. [Result](#E2E_Decision_Sdk_TelemetryTracingChannelMessage_Result) and [Error](#E2E_Decision_Sdk_TelemetryTracingChannelMessage_Error) are set, as Node does
on the message, before `asyncEnd` (and `error`) are published.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TracingChannel.cs)

## Properties

<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Error_"></a>
<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Error"></a>

### Error


```csharp
public object? Error { get; set; }
```


The operation's error, set before `error`.

**Value:** [object](https://learn.microsoft.com/dotnet/api/system.object)

<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Event_"></a>
<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Event"></a>

### Event


```csharp
public required object? Event { get; init; }
```


The operation's start event, with the telemetry settings and the AI SDK version.

**Value:** [object](https://learn.microsoft.com/dotnet/api/system.object)

<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Result_"></a>
<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Result"></a>

### Result


```csharp
public object? Result { get; set; }
```


The operation's result, set before `asyncStart` and `asyncEnd` when the call records its results.

**Value:** [object](https://learn.microsoft.com/dotnet/api/system.object)

<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Type_"></a>
<a id="E2E_Decision_Sdk_TelemetryTracingChannelMessage_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


One of [TelemetryTracingEventType](/api/E2E.Decision.Sdk/TelemetryTracingEventType/).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
