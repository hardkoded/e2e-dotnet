---
title: "IDecisionModel"
description: "A model that answers typed questions with probabilities (the AI SDK's Experimental_DecisionModelV4). Only specification version v4 is accepted."
editUrl: false
prev: false
next: false
sidebar:
  label: "IDecisionModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_IDecisionModel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface IDecisionModel
```


A model that answers typed questions with probabilities (the AI SDK's `Experimental_DecisionModelV4`). Only
specification version `v4` is accepted.

**Derived:** [EvaluationDecisionModel](/api/E2E.Decision.Sdk/EvaluationDecisionModel/), [GatewayDecisionModel](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModel/), [OpenAIDecisionModel](/api/E2E.Decision.Sdk/OpenAIDecisionModel/), [DecisionTypeSafeAiModel](/api/E2E.Decision.Sdk.TypeSafeAi/DecisionTypeSafeAiModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_IDecisionModel_ModelId_"></a>
<a id="E2E_Decision_Sdk_IDecisionModel_ModelId"></a>

### ModelId


```csharp
string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_IDecisionModel_Provider_"></a>
<a id="E2E_Decision_Sdk_IDecisionModel_Provider"></a>

### Provider


```csharp
string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_IDecisionModel_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_IDecisionModel_SpecificationVersion"></a>

### SpecificationVersion


```csharp
string SpecificationVersion { get; }
```


The specification version this model implements. Only `v4` is accepted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_IDecisionModel_SupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_IDecisionModel_SupportedQuestionTypes"></a>

### SupportedQuestionTypes


```csharp
IReadOnlyList<string> SupportedQuestionTypes { get; }
```


The question types this model can answer.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_IDecisionModel_DoDecideAsync_"></a>
<a id="E2E_Decision_Sdk_IDecisionModel_DoDecideAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

### DoDecideAsync(DecisionModelCall, CancellationToken)


```csharp
Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
```


Answers every question of &lt;code class="paramref"&gt;call&lt;/code&gt; in one request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionModelResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
