---
title: "IAgentModel"
description: "One turn of the agent model. Tool calls are executed by the agent, never by the model itself."
editUrl: false
prev: false
next: false
sidebar:
  label: "IAgentModel"
  hidden: true
---

<a id="E2E_IAgentModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IAgentModel
```


One turn of the agent model. Tool calls are executed by the agent, never by the model itself.

**Derived:** [AnthropicModel](/api/E2E/AnthropicModel/), [BedrockModel](/api/E2E/BedrockModel/), [GoogleModel](/api/E2E/GoogleModel/), [CopilotModel](/api/E2E.OAuth/CopilotModel/), [DelegatingModel](/api/E2E.OAuth/DelegatingModel/), [OpenCodeConsoleModel](/api/E2E.OAuth/OpenCodeConsoleModel/), [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/), [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/), [ScriptedModel](/api/E2E/ScriptedModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs)

## Properties

<a id="E2E_IAgentModel_Name_"></a>
<a id="E2E_IAgentModel_Name"></a>

### Name


```csharp
string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_IAgentModel_CompleteAsync_"></a>
<a id="E2E_IAgentModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

### CompleteAsync(ModelRequest, CancellationToken)


```csharp
Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [ModelRequest](/api/E2E/ModelRequest/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ModelResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
