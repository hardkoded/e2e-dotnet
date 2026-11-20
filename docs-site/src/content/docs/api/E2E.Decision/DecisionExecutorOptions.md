---
title: "DecisionExecutorOptions"
description: "Options for an executor that acts through a decision model and writes field text with a language model (upstream's DecisionExecutorOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionExecutorOptions"
  hidden: true
---

<a id="E2E_Decision_DecisionExecutorOptions"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class DecisionExecutorOptions
```


Options for an executor that acts through a decision model and writes field text with a language model (upstream's `DecisionExecutorOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Types.cs)

## Properties

<a id="E2E_Decision_DecisionExecutorOptions_EvaluationModel_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_EvaluationModel"></a>

### EvaluationModel


```csharp
public IEvaluationModel? EvaluationModel { get; init; }
```


A deprecated evaluation model, used when [Model](#E2E_Decision_DecisionExecutorOptions_Model) is not set. It runs through the same loop.

**Value:** [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/)

<a id="E2E_Decision_DecisionExecutorOptions_MinConfidence_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_MinConfidence"></a>

### MinConfidence


```csharp
public double MinConfidence { get; init; }
```


Minimum provider-reported confidence. Default 0 (off). A model that reports none counts as 0; a committed choice (an answer without a distribution) keeps the confidence the provider reports for it.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_DecisionExecutorOptions_MinProbability_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_MinProbability"></a>

### MinProbability


```csharp
public double MinProbability { get; init; }
```


Minimum probability for a selected operation, target, secret, or assertion verdict. Default 0 (off).

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_DecisionExecutorOptions_Model_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_Model"></a>

### Model


```csharp
public IDecisionModel? Model { get; init; }
```


A decision model that answers `choice` questions with a probability distribution, such as an [OpenAIDecisionModel](/api/E2E.Decision.Sdk/OpenAIDecisionModel/). Set this or [EvaluationModel](#E2E_Decision_DecisionExecutorOptions_EvaluationModel); the executor throws `INVALID_CONFIG` when neither is set.
An answer without [Probabilities](/api/E2E.Decision.Sdk/DecisionAnswer/#E2E_Decision_Sdk_DecisionAnswer_Probabilities) counts as a committed choice: probability 1, and the provider-reported confidence, which is 0 only when the provider reports none.

**Value:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_DecisionExecutorOptions_ProviderOptions_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Provider options sent with every decide call. They reach the decision model only: the text model keeps the agents entry's
`ProviderOptions`.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_DecisionExecutorOptions_TextModel_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_TextModel"></a>

### TextModel


```csharp
public IAgentModel? TextModel { get; init; }
```


A small language model that writes field values when the decision model picks `type`. It is exposed as the executor's model, so
the agent entry's judgment tier (`WaitFor`, `Extract`) uses it too. Without either, `type` is never offered.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

<a id="E2E_Decision_DecisionExecutorOptions_Vision_"></a>
<a id="E2E_Decision_DecisionExecutorOptions_Vision"></a>

### Vision


```csharp
public bool Vision { get; init; }
```


Ask for masked pixels on every observation. The model must take images and answer `score` questions. Default off.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
