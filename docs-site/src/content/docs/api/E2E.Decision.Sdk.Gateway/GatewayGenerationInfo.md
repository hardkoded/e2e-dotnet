---
title: "GatewayGenerationInfo"
description: "Upstream's GatewayGenerationInfo: the cost, token usage, latency, and provider of one generation."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayGenerationInfo"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayGenerationInfo : IEquatable<GatewayGenerationInfo>
```


Upstream's `GatewayGenerationInfo`: the cost, token usage, latency, and provider of one generation.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayGenerationInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayGenerationInfo.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_BillableWebSearchCalls_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_BillableWebSearchCalls"></a>

### BillableWebSearchCalls


```csharp
public required double BillableWebSearchCalls { get; init; }
```


Billable web search calls.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CacheCreationTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CacheCreationTokens"></a>

### CacheCreationTokens


```csharp
public required double CacheCreationTokens { get; init; }
```


Cache creation input tokens.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CachedTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CachedTokens"></a>

### CachedTokens


```csharp
public required double CachedTokens { get; init; }
```


Cached tokens used.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CompletionTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CompletionTokens"></a>

### CompletionTokens


```csharp
public required double CompletionTokens { get; init; }
```


Number of completion tokens.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CreatedAt_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_CreatedAt"></a>

### CreatedAt


```csharp
public required string CreatedAt { get; init; }
```


ISO 8601 timestamp when the generation was created.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_FinishReason_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_FinishReason"></a>

### FinishReason


```csharp
public required string FinishReason { get; init; }
```


Finish reason, such as `stop`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_GatewayCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_GatewayCost"></a>

### GatewayCost


```csharp
public double? GatewayCost { get; init; }
```


Total amount debited in USD. Null when unavailable.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_GenerationTime_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_GenerationTime"></a>

### GenerationTime


```csharp
public required double GenerationTime { get; init; }
```


Total generation time in milliseconds.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Id_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Id"></a>

### Id


```csharp
public required string Id { get; init; }
```


The generation ID.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_IsByok_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_IsByok"></a>

### IsByok


```csharp
public required bool IsByok { get; init; }
```


Whether BYOK credentials were used.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Latency_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Latency"></a>

### Latency


```csharp
public required double Latency { get; init; }
```


Time to first token in milliseconds.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_MarketCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_MarketCost"></a>

### MarketCost


```csharp
public double? MarketCost { get; init; }
```


Market cost in USD, excluding surcharges. Null when unavailable.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Model_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Model"></a>

### Model


```csharp
public required string Model { get; init; }
```


Model identifier.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_PromptTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_PromptTokens"></a>

### PromptTokens


```csharp
public required double PromptTokens { get; init; }
```


Number of prompt tokens.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_ProviderName_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_ProviderName"></a>

### ProviderName


```csharp
public required string ProviderName { get; init; }
```


Provider that served this generation.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_ReasoningTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_ReasoningTokens"></a>

### ReasoningTokens


```csharp
public required double ReasoningTokens { get; init; }
```


Reasoning tokens used.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Streamed_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Streamed"></a>

### Streamed


```csharp
public required bool Streamed { get; init; }
```


Whether streaming was used.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_SurchargeCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_SurchargeCost"></a>

### SurchargeCost


```csharp
public double? SurchargeCost { get; init; }
```


Total surcharges applied in USD. Null when unavailable.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_TotalCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_TotalCost"></a>

### TotalCost


```csharp
public required double TotalCost { get; init; }
```


Total cost in USD.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_UpstreamInferenceCost_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_UpstreamInferenceCost"></a>

### UpstreamInferenceCost


```csharp
public required double UpstreamInferenceCost { get; init; }
```


Upstream inference cost in USD (BYOK only).

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Usage_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_Usage"></a>

### Usage


```csharp
public required double Usage { get; init; }
```


Usage cost in USD (same as [TotalCost](#E2E_Decision_Sdk_Gateway_GatewayGenerationInfo_TotalCost)).

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)
