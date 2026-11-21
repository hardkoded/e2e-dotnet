---
title: "GatewaySpendReportRow"
description: "Upstream's GatewaySpendReportRow."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewaySpendReportRow"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewaySpendReportRow : IEquatable<GatewaySpendReportRow>
```


Upstream's `GatewaySpendReportRow`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewaySpendReportRow&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewaySpendReport.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_CacheCreationInputTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_CacheCreationInputTokens"></a>

### CacheCreationInputTokens


```csharp
public double? CacheCreationInputTokens { get; init; }
```


Number of cache creation input tokens.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_CachedInputTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_CachedInputTokens"></a>

### CachedInputTokens


```csharp
public double? CachedInputTokens { get; init; }
```


Number of cached input tokens.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_CredentialType_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_CredentialType"></a>

### CredentialType


```csharp
public string? CredentialType { get; init; }
```


Credential type, `byok` or `system` (present when `groupBy` is `credential_type`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Day_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Day"></a>

### Day


```csharp
public string? Day { get; init; }
```


Date string (present when `groupBy` is `day`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Hour_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Hour"></a>

### Hour


```csharp
public string? Hour { get; init; }
```


Hour timestamp (present when `groupBy` is `day` and `datePart` is `hour`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_InputTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_InputTokens"></a>

### InputTokens


```csharp
public double? InputTokens { get; init; }
```


Number of input tokens.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_MarketCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_MarketCost"></a>

### MarketCost


```csharp
public double? MarketCost { get; init; }
```


Market cost in USD.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Model_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Model"></a>

### Model


```csharp
public string? Model { get; init; }
```


Model identifier (present when `groupBy` is `model`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_OutputTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_OutputTokens"></a>

### OutputTokens


```csharp
public double? OutputTokens { get; init; }
```


Number of output tokens.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Provider_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Provider"></a>

### Provider


```csharp
public string? Provider { get; init; }
```


Provider name (present when `groupBy` is `provider`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_ReasoningTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_ReasoningTokens"></a>

### ReasoningTokens


```csharp
public double? ReasoningTokens { get; init; }
```


Number of reasoning tokens.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_RequestCount_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_RequestCount"></a>

### RequestCount


```csharp
public double? RequestCount { get; init; }
```


Number of requests.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Tag_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_Tag"></a>

### Tag


```csharp
public string? Tag { get; init; }
```


Tag value (present when `groupBy` is `tag`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_TotalCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_TotalCost"></a>

### TotalCost


```csharp
public required double TotalCost { get; init; }
```


Total cost in USD.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_User_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewaySpendReportRow_User"></a>

### User


```csharp
public string? User { get; init; }
```


User identifier (present when `groupBy` is `user`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
