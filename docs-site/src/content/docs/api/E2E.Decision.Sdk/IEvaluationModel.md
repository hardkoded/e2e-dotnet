---
title: "IEvaluationModel"
description: "A deprecated decision model shape (the AI SDK's Experimental_EvaluationModelV4): the same model with doEvaluate in place of doDecide. ResolveDecisionModel(IE..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEvaluationModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_IEvaluationModel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface IEvaluationModel
```


A deprecated decision model shape (the AI SDK's `Experimental_EvaluationModelV4`): the same model with `doEvaluate` in place
of `doDecide`. [ResolveDecisionModel(IEvaluationModel)](/api/E2E.Decision.Sdk/ResolveModel/#E2E_Decision_Sdk_ResolveModel_ResolveDecisionModel_E2E_Decision_Sdk_IEvaluationModel_) adapts it to an [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/).

**Derived:** [EvaluationDecisionModel](/api/E2E.Decision.Sdk/EvaluationDecisionModel/), [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/), [DecisionTypeSafeAiModel](/api/E2E.Decision.Sdk.TypeSafeAi/DecisionTypeSafeAiModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_IEvaluationModel_ModelId_"></a>
<a id="E2E_Decision_Sdk_IEvaluationModel_ModelId"></a>

### ModelId


```csharp
string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_IEvaluationModel_Provider_"></a>
<a id="E2E_Decision_Sdk_IEvaluationModel_Provider"></a>

### Provider


```csharp
string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_IEvaluationModel_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_IEvaluationModel_SpecificationVersion"></a>

### SpecificationVersion


```csharp
string SpecificationVersion { get; }
```


The specification version this model implements. Only `v4` is accepted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_IEvaluationModel_SupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_IEvaluationModel_SupportedQuestionTypes"></a>

### SupportedQuestionTypes


```csharp
IReadOnlyList<string> SupportedQuestionTypes { get; }
```


The question types this model can answer.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_IEvaluationModel_DoEvaluateAsync_"></a>
<a id="E2E_Decision_Sdk_IEvaluationModel_DoEvaluateAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

### DoEvaluateAsync(DecisionModelCall, CancellationToken)


```csharp
Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken)
```


Answers every question of &lt;code class="paramref"&gt;call&lt;/code&gt; in one request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionModelResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
