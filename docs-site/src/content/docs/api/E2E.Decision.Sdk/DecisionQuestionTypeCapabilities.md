---
title: "DecisionQuestionTypeCapabilities"
description: "The decision question types of ModelCapabilities.decision.questionTypes."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionQuestionTypeCapabilities"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionQuestionTypeCapabilities : IEquatable<DecisionQuestionTypeCapabilities>
```


The decision question types of `ModelCapabilities.decision.questionTypes`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionQuestionTypeCapabilities&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities__ctor_E2E_Decision_Sdk_ModelCapability_E2E_Decision_Sdk_ModelCapability_E2E_Decision_Sdk_ModelCapability_E2E_Decision_Sdk_ModelCapability_"></a>

### DecisionQuestionTypeCapabilities(ModelCapability, ModelCapability, ModelCapability, ModelCapability)


```csharp
public DecisionQuestionTypeCapabilities(ModelCapability Boolean, ModelCapability Choice, ModelCapability Score, ModelCapability Box)
```


The decision question types of `ModelCapabilities.decision.questionTypes`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Boolean` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |
| `Choice` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |
| `Score` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |
| `Box` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Boolean_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Boolean"></a>

### Boolean


```csharp
public ModelCapability Boolean { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)

<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Box_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Box"></a>

### Box


```csharp
public ModelCapability Box { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)

<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Choice_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Choice"></a>

### Choice


```csharp
public ModelCapability Choice { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)

<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Score_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_Score"></a>

### Score


```csharp
public ModelCapability Score { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)
