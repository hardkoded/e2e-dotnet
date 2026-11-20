---
title: "DiagnosticsChannel"
description: ".NET-only stand-in for Node's node:diagnostics_channel, which the AI SDK's tracing channel publishes to (tracing-channel-publisher.ts). A tracing channel is..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DiagnosticsChannel"
  hidden: true
---

<a id="E2E_Decision_Sdk_DiagnosticsChannel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class DiagnosticsChannel
```


.NET-only stand-in for Node's `node:diagnostics_channel`, which the AI SDK's tracing channel publishes to
(`tracing-channel-publisher.ts`). A tracing channel is found by name, and every caller that asks for a name gets the same one.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DiagnosticsChannel.cs)

## Methods

<a id="E2E_Decision_Sdk_DiagnosticsChannel_GetTracingChannel_"></a>
<a id="E2E_Decision_Sdk_DiagnosticsChannel_GetTracingChannel_System_String_"></a>

### GetTracingChannel(string)


```csharp
public static TracingChannel GetTracingChannel(string name)
```


`diagnosticsChannel.tracingChannel(name)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [TracingChannel](/api/E2E.Decision.Sdk/TracingChannel/)
