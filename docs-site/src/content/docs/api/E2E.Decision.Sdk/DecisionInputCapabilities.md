---
title: "DecisionInputCapabilities"
description: "The decision inputs of ModelCapabilities.decision.inputs."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionInputCapabilities"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionInputCapabilities"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionInputCapabilities : IEquatable<DecisionInputCapabilities>
```


The decision inputs of `ModelCapabilities.decision.inputs`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionInputCapabilities&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionInputCapabilities__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionInputCapabilities__ctor_E2E_Decision_Sdk_ModelCapability_"></a>

### DecisionInputCapabilities(ModelCapability)


```csharp
public DecisionInputCapabilities(ModelCapability Image)
```


The decision inputs of `ModelCapabilities.decision.inputs`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Image` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionInputCapabilities_Image_"></a>
<a id="E2E_Decision_Sdk_DecisionInputCapabilities_Image"></a>

### Image


```csharp
public ModelCapability Image { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)
