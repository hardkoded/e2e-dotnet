---
title: "GatewayLanguageModelEntry"
description: "The AI SDK's GatewayLanguageModelEntry: a model the Gateway offers."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayLanguageModelEntry"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayLanguageModelEntry : IEquatable<GatewayLanguageModelEntry>
```


The AI SDK's `GatewayLanguageModelEntry`: a model the Gateway offers.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayLanguageModelEntry&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayModelEntry.cs)

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Description_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Description"></a>

### Description


```csharp
public string? Description { get; init; }
```


Optional description of the model.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Id_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Id"></a>

### Id


```csharp
public required string Id { get; init; }
```


The model id used in model settings and to name the intended model.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_ModelType_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_ModelType"></a>

### ModelType


```csharp
public string? ModelType { get; init; }
```


One of [KnownModelTypes](/api/E2E.Decision.Sdk.Gateway/GatewayModelEntryTypes/#E2E_Decision_Sdk_Gateway_GatewayModelEntryTypes_KnownModelTypes), to tell the model types apart.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


The display name of the model.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Pricing_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Pricing"></a>

### Pricing


```csharp
public GatewayModelPricing? Pricing { get; init; }
```


Optional pricing information for the model.

**Value:** [GatewayModelPricing](/api/E2E.Decision.Sdk.Gateway/GatewayModelPricing/)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Specification_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry_Specification"></a>

### Specification


```csharp
public required GatewayLanguageModelSpecification Specification { get; init; }
```


The AI SDK language model specification of the model.

**Value:** [GatewayLanguageModelSpecification](/api/E2E.Decision.Sdk.Gateway/GatewayLanguageModelSpecification/)
