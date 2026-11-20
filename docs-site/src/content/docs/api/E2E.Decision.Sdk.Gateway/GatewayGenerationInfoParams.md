---
title: "GatewayGenerationInfoParams"
description: "Upstream's GatewayGenerationInfoParams."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayGenerationInfoParams"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoParams"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayGenerationInfoParams
```


Upstream's `GatewayGenerationInfoParams`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayGenerationInfo.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoParams_Id_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayGenerationInfoParams_Id"></a>

### Id


```csharp
public required string Id { get; init; }
```


The generation ID to look up (format: `gen_<ulid>`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
