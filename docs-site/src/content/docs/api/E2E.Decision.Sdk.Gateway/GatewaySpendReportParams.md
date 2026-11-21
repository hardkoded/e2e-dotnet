---
title: "GatewaySpendReportParams"
description: "Upstream's GatewaySpendReportParams."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewaySpendReportParams"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewaySpendReportParams
```


Upstream's `GatewaySpendReportParams`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewaySpendReport.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_CredentialType_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_CredentialType"></a>

### CredentialType


```csharp
public string? CredentialType { get; init; }
```


Filter to `byok` or `system` credentials.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_DatePart_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_DatePart"></a>

### DatePart


```csharp
public string? DatePart { get; init; }
```


Time granularity when [GroupBy](#E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_GroupBy) is `day`: `day` or `hour`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_EndDate_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_EndDate"></a>

### EndDate


```csharp
public required string EndDate { get; init; }
```


End date in YYYY-MM-DD format (inclusive).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_GroupBy_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_GroupBy"></a>

### GroupBy


```csharp
public string? GroupBy { get; init; }
```


Primary aggregation dimension: `day`, `user`, `model`, `tag`, `provider`, or `credential_type`. Defaults to `day`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_Model_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_Model"></a>

### Model


```csharp
public string? Model { get; init; }
```


Filter to a specific model, such as `anthropic/claude-sonnet-4.5`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_Provider_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_Provider"></a>

### Provider


```csharp
public string? Provider { get; init; }
```


Filter to a specific provider, such as `anthropic`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_StartDate_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_StartDate"></a>

### StartDate


```csharp
public required string StartDate { get; init; }
```


Start date in YYYY-MM-DD format (inclusive).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_Tags_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_Tags"></a>

### Tags


```csharp
public IReadOnlyList<string>? Tags { get; init; }
```


Filter to requests with these tags.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_UserId_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportParams_UserId"></a>

### UserId


```csharp
public string? UserId { get; init; }
```


Filter to a specific user's spend.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
