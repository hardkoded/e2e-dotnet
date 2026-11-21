---
title: "TelemetryRegistry"
description: "The AI SDK's global telemetry integration registry (telemetry/telemetry-registry.ts). A decide call that sets no Integrations uses the integrations registere..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TelemetryRegistry"
  hidden: true
---

<a id="E2E_Decision_Sdk_TelemetryRegistry"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class TelemetryRegistry
```


The AI SDK's global telemetry integration registry (`telemetry/telemetry-registry.ts`). A decide call that sets no
[Integrations](/api/E2E.Decision.Sdk/TelemetryOptions/#E2E_Decision_Sdk_TelemetryOptions_Integrations) uses the integrations registered here.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TelemetryRegistry.cs)

## Methods

<a id="E2E_Decision_Sdk_TelemetryRegistry_GetGlobalTelemetryIntegrations_"></a>
<a id="E2E_Decision_Sdk_TelemetryRegistry_GetGlobalTelemetryIntegrations"></a>

### GetGlobalTelemetryIntegrations()


```csharp
public static IReadOnlyList<ITelemetry> GetGlobalTelemetryIntegrations()
```


The globally registered telemetry integrations, in registration order (`getGlobalTelemetryIntegrations`).

**Returns:** [IReadOnlyList&lt;ITelemetry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_TelemetryRegistry_RegisterTelemetry_"></a>
<a id="E2E_Decision_Sdk_TelemetryRegistry_RegisterTelemetry_E2E_Decision_Sdk_ITelemetry___"></a>

### RegisterTelemetry(params ITelemetry[])


```csharp
public static void RegisterTelemetry(params ITelemetry[] integrations)
```


Registers one or more telemetry integrations globally (`registerTelemetry`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `integrations` | `ITelemetry\[\]` |  |
