---
title: "DecisionCapabilities"
description: "Upstream's ModelCapabilities.decision."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionCapabilities"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionCapabilities"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionCapabilities : IEquatable<DecisionCapabilities>
```


Upstream's `ModelCapabilities.decision`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionCapabilities&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionCapabilities__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionCapabilities__ctor_E2E_Decision_Sdk_DecisionQuestionTypeCapabilities_E2E_Decision_Sdk_DecisionInputCapabilities_E2E_Decision_Sdk_ModelCapability_E2E_Decision_Sdk_ModelCapability_"></a>

### DecisionCapabilities(DecisionQuestionTypeCapabilities, DecisionInputCapabilities, ModelCapability, ModelCapability)


```csharp
public DecisionCapabilities(DecisionQuestionTypeCapabilities QuestionTypes, DecisionInputCapabilities Inputs, ModelCapability Reasoning, ModelCapability MaxOutputTokens)
```


Upstream's `ModelCapabilities.decision`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `QuestionTypes` | [DecisionQuestionTypeCapabilities](/api/E2E.Decision.Sdk/DecisionQuestionTypeCapabilities/) |  |
| `Inputs` | [DecisionInputCapabilities](/api/E2E.Decision.Sdk/DecisionInputCapabilities/) |  |
| `Reasoning` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |
| `MaxOutputTokens` | [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionCapabilities_Description_"></a>
<a id="E2E_Decision_Sdk_DecisionCapabilities_Description"></a>

### Description


```csharp
public string? Description { get; init; }
```


A model description, which TypeSafe's catalog adds to the decision section.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionCapabilities_Inputs_"></a>
<a id="E2E_Decision_Sdk_DecisionCapabilities_Inputs"></a>

### Inputs


```csharp
public DecisionInputCapabilities Inputs { get; init; }
```


**Value:** [DecisionInputCapabilities](/api/E2E.Decision.Sdk/DecisionInputCapabilities/)

<a id="E2E_Decision_Sdk_DecisionCapabilities_MaxOutputTokens_"></a>
<a id="E2E_Decision_Sdk_DecisionCapabilities_MaxOutputTokens"></a>

### MaxOutputTokens


```csharp
public ModelCapability MaxOutputTokens { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)

<a id="E2E_Decision_Sdk_DecisionCapabilities_QuestionTypes_"></a>
<a id="E2E_Decision_Sdk_DecisionCapabilities_QuestionTypes"></a>

### QuestionTypes


```csharp
public DecisionQuestionTypeCapabilities QuestionTypes { get; init; }
```


**Value:** [DecisionQuestionTypeCapabilities](/api/E2E.Decision.Sdk/DecisionQuestionTypeCapabilities/)

<a id="E2E_Decision_Sdk_DecisionCapabilities_Reasoning_"></a>
<a id="E2E_Decision_Sdk_DecisionCapabilities_Reasoning"></a>

### Reasoning


```csharp
public ModelCapability Reasoning { get; init; }
```


**Value:** [ModelCapability](/api/E2E.Decision.Sdk/ModelCapability/)
