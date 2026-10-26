---
title: "OpenAiCompatibleModel"
description: "Chat-completions client for OpenAI and any server that speaks the same tool-call JSON. Set BaseUrl to a local server such as http://127.0.0.1:11434/v1. The A..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenAiCompatibleModel"
  hidden: true
---

<a id="E2E_OpenAiCompatibleModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class OpenAiCompatibleModel : IAgentModel, IDisposable
```


Chat-completions client for OpenAI and any server that speaks the same tool-call JSON.
Set [BaseUrl](/api/E2E/OpenAiCompatibleModelOptions/#E2E_OpenAiCompatibleModelOptions_BaseUrl) to a local server such as
`http://127.0.0.1:11434/v1`. The API key is optional for servers that do not check one.
[ModelProviders](/api/E2E/ModelProviders/) has presets for OpenRouter, the Vercel AI Gateway, SpaceXAI, and Ollama.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L18)

## Constructors

<a id="E2E_OpenAiCompatibleModel__ctor_"></a>
<a id="E2E_OpenAiCompatibleModel__ctor_E2E_OpenAiCompatibleModelOptions_System_Net_Http_HttpClient_"></a>

### OpenAiCompatibleModel(OpenAiCompatibleModelOptions, HttpClient?)


```csharp
public OpenAiCompatibleModel(OpenAiCompatibleModelOptions options, HttpClient? httpClient = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [OpenAiCompatibleModelOptions](/api/E2E/OpenAiCompatibleModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L24)

## Properties

<a id="E2E_OpenAiCompatibleModel_Name_"></a>
<a id="E2E_OpenAiCompatibleModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L41)

## Methods

<a id="E2E_OpenAiCompatibleModel_CompleteAsync_"></a>
<a id="E2E_OpenAiCompatibleModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L43)

<a id="E2E_OpenAiCompatibleModel_Dispose_"></a>
<a id="E2E_OpenAiCompatibleModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/OpenAiCompatibleModel.cs#L54)
