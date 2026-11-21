---
title: "GatewaySpendReport"
description: "The AI SDK's GatewaySpendReport (gateway-spend-report.ts): GET {origin}/v1/report."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewaySpendReport"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReport"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewaySpendReport
```


The AI SDK's `GatewaySpendReport` (`gateway-spend-report.ts`): `GET {origin}/v1/report`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewaySpendReport.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReport__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReport__ctor_E2E_Decision_Sdk_Gateway_GatewayConfig_"></a>

### GatewaySpendReport(GatewayConfig)


```csharp
public GatewaySpendReport(GatewayConfig config)
```


The AI SDK's `GatewaySpendReport` (`gateway-spend-report.ts`): `GET {origin}/v1/report`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `config` | [GatewayConfig](/api/E2E.Decision.Sdk.Gateway/GatewayConfig/) |  |

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReport_GetSpendReportAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReport_GetSpendReportAsync_E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_System_Threading_CancellationToken_"></a>

### GetSpendReportAsync(GatewaySpendReportParams, CancellationToken)


```csharp
public Task<GatewaySpendReportResponse> GetSpendReportAsync(GatewaySpendReportParams parameters, CancellationToken cancellationToken = default)
```


Upstream's `getSpendReport`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `parameters` | [GatewaySpendReportParams](/api/E2E.Decision.Sdk.Gateway/GatewaySpendReportParams/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;GatewaySpendReportResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
