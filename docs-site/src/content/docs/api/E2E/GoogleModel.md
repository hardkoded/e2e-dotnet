---
title: "GoogleModel"
description: "Google Gemini API (Generative Language) client. Tool schemas go as parametersJsonSchema, so the agent's JSON Schemas need no conversion. Thought signatures G..."
editUrl: false
prev: false
next: false
sidebar:
  label: "GoogleModel"
  hidden: true
---

<a id="E2E_GoogleModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class GoogleModel : IAgentModel, IDisposable
```


Google Gemini API (Generative Language) client. Tool schemas go as `parametersJsonSchema`, so the agent's
JSON Schemas need no conversion. Thought signatures Gemini attaches to a function call are sent back with it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L17)

## Constructors

<a id="E2E_GoogleModel__ctor_"></a>
<a id="E2E_GoogleModel__ctor_E2E_GoogleModelOptions_System_Net_Http_HttpClient_"></a>

### GoogleModel(GoogleModelOptions, HttpClient?)


```csharp
public GoogleModel(GoogleModelOptions options, HttpClient? httpClient = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [GoogleModelOptions](/api/E2E/GoogleModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L28)

## Properties

<a id="E2E_GoogleModel_Name_"></a>
<a id="E2E_GoogleModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L37)

## Methods

<a id="E2E_GoogleModel_CompleteAsync_"></a>
<a id="E2E_GoogleModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L39)

<a id="E2E_GoogleModel_Dispose_"></a>
<a id="E2E_GoogleModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/GoogleModel.cs#L56)
