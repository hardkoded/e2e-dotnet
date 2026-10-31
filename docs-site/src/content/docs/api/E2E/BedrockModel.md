---
title: "BedrockModel"
description: "Amazon Bedrock Converse API client. It signs with AWS Signature Version 4 from the standard AWS environment variables or, with none set, from the AWS credent..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BedrockModel"
  hidden: true
---

<a id="E2E_BedrockModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BedrockModel : IAgentModel, IDisposable
```


Amazon Bedrock Converse API client. It signs with AWS Signature Version 4 from the standard AWS environment
variables or, with none set, from the AWS credential chain (shared profile, SSO, container and instance roles), as the AI SDK's
`amazon-bedrock` provider does, or sends a Bedrock API key (`AWS_BEARER_TOKEN_BEDROCK`) as a bearer token when one is set.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L20)

## Constructors

<a id="E2E_BedrockModel__ctor_"></a>
<a id="E2E_BedrockModel__ctor_E2E_BedrockModelOptions_System_Net_Http_HttpClient_"></a>

### BedrockModel(BedrockModelOptions, HttpClient?)


```csharp
public BedrockModel(BedrockModelOptions options, HttpClient? httpClient = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [BedrockModelOptions](/api/E2E/BedrockModelOptions/) |  |
| `httpClient` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L29)

## Properties

<a id="E2E_BedrockModel_Name_"></a>
<a id="E2E_BedrockModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L38)

## Methods

<a id="E2E_BedrockModel_CompleteAsync_"></a>
<a id="E2E_BedrockModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L40)

<a id="E2E_BedrockModel_Dispose_"></a>
<a id="E2E_BedrockModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L104)
