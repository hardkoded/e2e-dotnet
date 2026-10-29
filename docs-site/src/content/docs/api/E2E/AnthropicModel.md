---
title: "AnthropicModel"
description: "Anthropic Messages API client. The system prompt and the newest message carry cache breakpoints, as upstream marks them, so each turn of a tool loop reads th..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AnthropicModel"
  hidden: true
---

<a id="E2E_AnthropicModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AnthropicModel : IAgentModel, IDisposable
```


Anthropic Messages API client. The system prompt and the newest message carry cache breakpoints, as
upstream marks them, so each turn of a tool loop reads the previous turn's prefix from the cache.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L15)

## Constructors

<a id="E2E_AnthropicModel__ctor_"></a>
<a id="E2E_AnthropicModel__ctor_E2E_AnthropicModelOptions_System_Net_Http_HttpClient_"></a>

### AnthropicModel(AnthropicModelOptions, HttpClient?)


```csharp
public AnthropicModel(AnthropicModelOptions options, HttpClient? httpClient = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [AnthropicModelOptions](/api/E2E/AnthropicModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L23)

## Properties

<a id="E2E_AnthropicModel_Name_"></a>
<a id="E2E_AnthropicModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L32)

## Methods

<a id="E2E_AnthropicModel_CompleteAsync_"></a>
<a id="E2E_AnthropicModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L34)

<a id="E2E_AnthropicModel_Dispose_"></a>
<a id="E2E_AnthropicModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/AnthropicModel.cs#L52)
