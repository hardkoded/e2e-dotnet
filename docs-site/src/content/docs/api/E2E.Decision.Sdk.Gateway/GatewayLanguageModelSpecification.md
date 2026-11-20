---
title: "GatewayLanguageModelSpecification"
description: "Upstream's GatewayLanguageModelSpecification: Pick<LanguageModelV4, 'specificationVersion' | 'provider' | 'modelId'>."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayLanguageModelSpecification"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayLanguageModelSpecification : IEquatable<GatewayLanguageModelSpecification>
```


Upstream's `GatewayLanguageModelSpecification`: `Pick<LanguageModelV4, 'specificationVersion' | 'provider' | 'modelId'>`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayLanguageModelSpecification&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayModelEntry.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification__ctor_System_String_System_String_System_String_"></a>

### GatewayLanguageModelSpecification(string, string, string)


```csharp
public GatewayLanguageModelSpecification(string SpecificationVersion, string Provider, string ModelId)
```


Upstream's `GatewayLanguageModelSpecification`: `Pick<LanguageModelV4, 'specificationVersion' | 'provider' | 'modelId'>`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `SpecificationVersion` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `ModelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification_ModelId_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification_Provider_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayLanguageModelSpecification_SpecificationVersion"></a>

### SpecificationVersion


```csharp
public string SpecificationVersion { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
