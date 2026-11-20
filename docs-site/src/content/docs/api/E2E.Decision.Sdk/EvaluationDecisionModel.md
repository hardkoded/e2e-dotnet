---
title: "EvaluationDecisionModel"
description: "A decision model that also has the deprecated doEvaluate (upstream's DecisionModelV4 & { doEvaluate }, what asEvaluationModel returns). .NET-only: C# needs a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EvaluationDecisionModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_EvaluationDecisionModel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class EvaluationDecisionModel : IDecisionModel, IEvaluationModel
```


A decision model that also has the deprecated `doEvaluate` (upstream's `DecisionModelV4 & { doEvaluate }`, what
`asEvaluationModel` returns). .NET-only: C# needs a named type for the intersection.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/), [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ResolveModel.cs)

## Constructors

<a id="E2E_Decision_Sdk_EvaluationDecisionModel__ctor_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel__ctor_E2E_Decision_Sdk_IDecisionModel_"></a>

### EvaluationDecisionModel(IDecisionModel)


```csharp
public EvaluationDecisionModel(IDecisionModel model)
```


A decision model that also has the deprecated `doEvaluate` (upstream's `DecisionModelV4 & { doEvaluate }`, what
`asEvaluationModel` returns). .NET-only: C# needs a named type for the intersection.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/) |  |

## Properties

<a id="E2E_Decision_Sdk_EvaluationDecisionModel_ModelId_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_EvaluationDecisionModel_Provider_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel_Provider"></a>

### Provider


```csharp
public string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_EvaluationDecisionModel_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel_SpecificationVersion"></a>

### SpecificationVersion


```csharp
public string SpecificationVersion { get; }
```


The specification version this model implements. Only `v4` is accepted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_EvaluationDecisionModel_SupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel_SupportedQuestionTypes"></a>

### SupportedQuestionTypes


```csharp
public IReadOnlyList<string> SupportedQuestionTypes { get; }
```


The question types this model can answer.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_EvaluationDecisionModel_DoDecideAsync_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel_DoDecideAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

### DoDecideAsync(DecisionModelCall, CancellationToken)


```csharp
public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
```


Answers every question of &lt;code class="paramref"&gt;call&lt;/code&gt; in one request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionModelResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_EvaluationDecisionModel_DoEvaluateAsync_"></a>
<a id="E2E_Decision_Sdk_EvaluationDecisionModel_DoEvaluateAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

### DoEvaluateAsync(DecisionModelCall, CancellationToken)


```csharp
public Task<DecisionModelResult> DoEvaluateAsync(DecisionModelCall call, CancellationToken cancellationToken)
```


Deprecated: use `DoDecideAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionModelResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
