---
title: "GatewaySpendReportResponse"
description: "Upstream's GatewaySpendReportResponse."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewaySpendReportResponse"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportResponse"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewaySpendReportResponse : IEquatable<GatewaySpendReportResponse>
```


Upstream's `GatewaySpendReportResponse`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewaySpendReportResponse&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewaySpendReport.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportResponse__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportResponse__ctor_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_Gateway_GatewaySpendReportRow__"></a>

### `GatewaySpendReportResponse(IReadOnlyList<GatewaySpendReportRow>)`


```csharp
public GatewaySpendReportResponse(IReadOnlyList<GatewaySpendReportRow> Results)
```


Upstream's `GatewaySpendReportResponse`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Results` | [IReadOnlyList&lt;GatewaySpendReportRow&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportResponse_Results_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportResponse_Results"></a>

### Results


```csharp
public IReadOnlyList<GatewaySpendReportRow> Results { get; init; }
```


**Value:** [IReadOnlyList&lt;GatewaySpendReportRow&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
