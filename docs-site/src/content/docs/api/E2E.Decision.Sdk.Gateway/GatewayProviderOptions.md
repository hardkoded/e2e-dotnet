---
title: "GatewayProviderOptions"
description: "The AI SDK's gateway-provider-options.ts: the check of the gateway provider options a decision call takes. The models fallback list is validated (at most one..."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayProviderOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderOptions"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public static class GatewayProviderOptions
```


The AI SDK's `gateway-provider-options.ts`: the check of the `gateway` provider options a decision call takes. The
`models` fallback list is validated (at most one conditional entry, and it comes first); every other option is the Gateway's own and
passes through.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayProviderOptions.cs)

## Fields

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderOptions_DecisionFallbackMaxConditionDepth"></a>

### DecisionFallbackMaxConditionDepth


```csharp
public const int DecisionFallbackMaxConditionDepth = 5
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderOptions_DecisionFallbackMaxConditionsPerList"></a>

### DecisionFallbackMaxConditionsPerList


```csharp
public const int DecisionFallbackMaxConditionsPerList = 20
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderOptions_DecisionFallbackMaxQuestionLength"></a>

### DecisionFallbackMaxQuestionLength


```csharp
public const int DecisionFallbackMaxQuestionLength = 256
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayProviderOptions_ValidateDecisionProviderOptions_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayProviderOptions_ValidateDecisionProviderOptions_System_Text_Json_JsonElement_"></a>

### ValidateDecisionProviderOptions(JsonElement)


```csharp
public static string? ValidateDecisionProviderOptions(JsonElement options)
```


Upstream's `gatewayDecisionProviderOptionsSchema`, as a check: null when &lt;code class="paramref"&gt;options&lt;/code&gt; is valid, else the first problem
found.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
