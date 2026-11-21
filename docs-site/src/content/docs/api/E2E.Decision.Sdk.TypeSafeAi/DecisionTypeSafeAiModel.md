---
title: "DecisionTypeSafeAiModel"
description: "Answers questions through TypeSafe's SystemOne API, POST /systemone (the AI SDK's DecisionTypeSafeAiModel in @ai-sdk/typesafe-ai). It answers choice, score,..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionTypeSafeAiModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed class DecisionTypeSafeAiModel : IDecisionModel, IEvaluationModel, IDisposable
```


Answers questions through TypeSafe's SystemOne API, `POST /systemone` (the AI SDK's `DecisionTypeSafeAiModel` in
`@ai-sdk/typesafe-ai`). It answers `choice`, `score`, `boolean`, and `box` questions, takes PNG and JPEG images, and
reports each answer's confidence as `typesafe` provider metadata.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/), [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiDecisionModel.cs)

## Constructors

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel__ctor_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel__ctor_System_String_E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_"></a>

### DecisionTypeSafeAiModel(string, TypeSafeAiDecisionModelConfig)


```csharp
public DecisionTypeSafeAiModel(string modelId, TypeSafeAiDecisionModelConfig config)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `config` | [TypeSafeAiDecisionModelConfig](/api/E2E.Decision.Sdk.TypeSafeAi/TypeSafeAiDecisionModelConfig/) |  |

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_ModelId_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_Provider_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_Provider"></a>

### Provider


```csharp
public string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_SpecificationVersion"></a>

### SpecificationVersion


```csharp
public string SpecificationVersion { get; }
```


The specification version this model implements. Only `v4` is accepted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_SupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_SupportedQuestionTypes"></a>

### SupportedQuestionTypes


```csharp
public IReadOnlyList<string> SupportedQuestionTypes { get; }
```


The question types this model can answer.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_Dispose_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_DoDecideAsync_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_DoDecideAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

### DoDecideAsync(DecisionModelCall, CancellationToken)


```csharp
public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
```


Sends every question in one request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionModelResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_DoEvaluateAsync_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_DoEvaluateAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

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

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_WorkflowDeserialize_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_WorkflowDeserialize_E2E_Decision_Sdk_TypeSafeAi_SerializedModelOptions_System_Net_Http_HttpClient_"></a>

### WorkflowDeserialize(SerializedModelOptions, HttpClient?)


```csharp
public static DecisionTypeSafeAiModel WorkflowDeserialize(SerializedModelOptions options, HttpClient? httpClient = null)
```


Restores a model from [WorkflowSerialize(DecisionTypeSafeAiModel)](#E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_WorkflowSerialize_E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_) (upstream's `[WORKFLOW_DESERIALIZE]`). Without serialized headers the model reads
`TYPESAFE_AI_API_KEY` at each call. &lt;code class="paramref"&gt;httpClient&lt;/code&gt; stands in for the `fetch` a config can carry upstream.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [SerializedModelOptions](/api/E2E.Decision.Sdk.TypeSafeAi/SerializedModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

**Returns:** [DecisionTypeSafeAiModel](./)

<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_WorkflowSerialize_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_WorkflowSerialize_E2E_Decision_Sdk_TypeSafeAi_DecisionTypeSafeAiModel_"></a>

### WorkflowSerialize(DecisionTypeSafeAiModel)


```csharp
public static SerializedModelOptions WorkflowSerialize(DecisionTypeSafeAiModel model)
```


Serializes a model for a workflow step boundary (upstream's `[WORKFLOW_SERIALIZE]`). The client is not serializable, so it is omitted.
The headers function is resolved now and kept when it completes synchronously.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [DecisionTypeSafeAiModel](./) |  |

**Returns:** [SerializedModelOptions](/api/E2E.Decision.Sdk.TypeSafeAi/SerializedModelOptions/)
