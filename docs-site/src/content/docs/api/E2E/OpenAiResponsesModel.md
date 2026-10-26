---
title: "OpenAiResponsesModel"
description: "OpenAI Responses API client: OpenAI itself, Azure OpenAI (String)), and the subscription backends that only speak Responses. Nothing is stored server side, s..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenAiResponsesModel"
  hidden: true
---

<a id="E2E_OpenAiResponsesModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class OpenAiResponsesModel : IAgentModel, IDisposable
```


OpenAI Responses API client: OpenAI itself, Azure OpenAI (`String)`), and the
subscription backends that only speak Responses. Nothing is stored server side, so each turn carries the
whole conversation. A server-sent-event answer is folded back into the completed response.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L16)

## Constructors

<a id="E2E_OpenAiResponsesModel__ctor_"></a>
<a id="E2E_OpenAiResponsesModel__ctor_E2E_OpenAiResponsesModelOptions_System_Net_Http_HttpClient_"></a>

### OpenAiResponsesModel(OpenAiResponsesModelOptions, HttpClient?)


```csharp
public OpenAiResponsesModel(OpenAiResponsesModelOptions options, HttpClient? httpClient = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [OpenAiResponsesModelOptions](/api/E2E/OpenAiResponsesModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L22)

## Properties

<a id="E2E_OpenAiResponsesModel_Name_"></a>
<a id="E2E_OpenAiResponsesModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L31)

## Methods

<a id="E2E_OpenAiResponsesModel_CompleteAsync_"></a>
<a id="E2E_OpenAiResponsesModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

### CompleteAsync(ModelRequest, CancellationToken)


```csharp
public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [ModelRequest](/api/E2E/ModelRequest/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ModelResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L33)

<a id="E2E_OpenAiResponsesModel_Dispose_"></a>
<a id="E2E_OpenAiResponsesModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/OpenAiResponsesModel.cs#L50)
