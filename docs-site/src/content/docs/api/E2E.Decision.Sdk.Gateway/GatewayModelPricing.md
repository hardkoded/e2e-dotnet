---
title: "GatewayModelPricing"
description: "Upstream's GatewayLanguageModelEntry.pricing: the cost of a model in USD."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayModelPricing"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayModelPricing : IEquatable<GatewayModelPricing>
```


Upstream's `GatewayLanguageModelEntry.pricing`: the cost of a model in USD.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayModelPricing&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayModelEntry.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_CacheCreationInputTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_CacheCreationInputTokens"></a>

### CacheCreationInputTokens


```csharp
public string? CacheCreationInputTokens { get; init; }
```


Cost per input token to create cache entries in USD. Only present for models that support prompt caching.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_CachedInputTokens_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_CachedInputTokens"></a>

### CachedInputTokens


```csharp
public string? CachedInputTokens { get; init; }
```


Cost per cached input token in USD. Only present for models that support prompt caching.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_Input_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_Input"></a>

### Input


```csharp
public required string Input { get; init; }
```


Cost per input token in USD.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_Output_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelPricing_Output"></a>

### Output


```csharp
public required string Output { get; init; }
```


Cost per output token in USD.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
