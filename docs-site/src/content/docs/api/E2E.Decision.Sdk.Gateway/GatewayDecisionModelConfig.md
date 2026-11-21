---
title: "GatewayDecisionModelConfig"
description: "The config of a GatewayDecisionModel (upstream's GatewayConfig & { provider, o11yHeaders })."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayDecisionModelConfig"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModelConfig"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayDecisionModelConfig : GatewayConfig
```


The config of a [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/) (upstream's `GatewayConfig & { provider, o11yHeaders }`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [GatewayConfig](/api/E2E.Decision.Sdk.Gateway/GatewayConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayDecisionModel.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModelConfig_O11yHeaders_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModelConfig_O11yHeaders"></a>

### O11yHeaders


```csharp
public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> O11yHeaders { get; init; }
```


Resolves the observability headers (`ai-o11y-*`) sent with every request.

**Value:** [Func&lt;CancellationToken, Task&lt;IReadOnlyDictionary&lt;string, string&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModelConfig_Provider_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModelConfig_Provider"></a>

### Provider


```csharp
public required string Provider { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
