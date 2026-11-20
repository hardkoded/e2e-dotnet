---
title: "GatewayDecisionModel"
description: "Answers questions through the Vercel AI Gateway, POST /decision-model (the AI SDK's GatewayDecisionModel in @ai-sdk/gateway). It answers choice, score, and b..."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayDecisionModel"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayDecisionModel : IDecisionModel, IEvaluationModel, IDisposable
```


Answers questions through the Vercel AI Gateway, `POST /decision-model` (the AI SDK's `GatewayDecisionModel` in
`@ai-sdk/gateway`). It answers `choice`, `score`, and `boolean` questions; the Gateway routes the call to the model
named by the id, such as `typesafe-ai/jev`, and applies the `gateway` provider options (fallbacks, routing).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/), [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayDecisionModel.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel__ctor_System_String_E2E_Decision_Sdk_Gateway_GatewayDecisionModelConfig_"></a>

### GatewayDecisionModel(string, GatewayDecisionModelConfig)


```csharp
public GatewayDecisionModel(string modelId, GatewayDecisionModelConfig config)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `config` | [GatewayDecisionModelConfig](/api/E2E.Decision.Sdk.Gateway/GatewayDecisionModelConfig/) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_ModelId_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_ModelId"></a>

### ModelId


```csharp
public string ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_Provider_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_Provider"></a>

### Provider


```csharp
public string Provider { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_SpecificationVersion"></a>

### SpecificationVersion


```csharp
public string SpecificationVersion { get; }
```


The specification version this model implements. Only `v4` is accepted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_SupportedQuestionTypes_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_SupportedQuestionTypes"></a>

### SupportedQuestionTypes


```csharp
public IReadOnlyList<string> SupportedQuestionTypes { get; }
```


The question types this model can answer.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_Dispose_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_DoDecideAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_DoDecideAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

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

<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_DoEvaluateAsync_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayDecisionModel_DoEvaluateAsync_E2E_Decision_Sdk_DecisionModelCall_System_Threading_CancellationToken_"></a>

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
