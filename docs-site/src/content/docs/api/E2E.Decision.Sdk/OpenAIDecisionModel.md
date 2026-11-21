---
title: "OpenAIDecisionModel"
description: "Answers questions through the OpenAI Decisions API, POST /decisions (the AI SDK's OpenAIDecisionModel in @ai-sdk/openai). It answers choice, score, and boole..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenAIDecisionModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_OpenAIDecisionModel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class OpenAIDecisionModel : IDecisionModel, IDisposable
```


Answers questions through the OpenAI Decisions API, `POST /decisions` (the AI SDK's `OpenAIDecisionModel` in
`@ai-sdk/openai`). It answers `choice`, `score`, and `boolean` questions, and the answers carry the model's probabilities.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/OpenAIDecisionModel.cs)

## Constructors

<a id="E2E_Decision_Sdk_OpenAIDecisionModel__ctor_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel__ctor_System_String_E2E_Decision_Sdk_OpenAIDecisionModelOptions_System_Net_Http_HttpClient_"></a>

### OpenAIDecisionModel(string, OpenAIDecisionModelOptions?, HttpClient?)


```csharp
public OpenAIDecisionModel(string modelId, OpenAIDecisionModelOptions? options = null, HttpClient? httpClient = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [OpenAIDecisionModelOptions](/api/E2E.Decision.Sdk/OpenAIDecisionModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

## Properties

<a id="E2E_Decision_Sdk_OpenAIDecisionModel_ModelId_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_OpenAIDecisionModel_Provider_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel_Provider"></a>

### Provider


```csharp
public string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_OpenAIDecisionModel_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel_SpecificationVersion"></a>

### SpecificationVersion


```csharp
public string SpecificationVersion { get; }
```


The specification version this model implements. Only `v4` is accepted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_OpenAIDecisionModel_SupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel_SupportedQuestionTypes"></a>

### SupportedQuestionTypes


```csharp
public IReadOnlyList<string> SupportedQuestionTypes { get; }
```


The question types this model can answer.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_OpenAIDecisionModel_Dispose_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

<a id="E2E_Decision_Sdk_OpenAIDecisionModel_DoDecideAsync_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModel_DoDecideAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

### DoDecideAsync(DecisionModelCall, CancellationToken)


```csharp
public Task<DecisionModelResult> DoDecideAsync(DecisionModelCall call, CancellationToken cancellationToken)
```


Sends every question in one Decisions request.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `call` | [DecisionModelCall](/api/E2E.Decision.Sdk/DecisionModelCall/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionModelResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
