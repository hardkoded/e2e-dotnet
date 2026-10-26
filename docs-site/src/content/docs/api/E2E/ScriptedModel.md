---
title: "ScriptedModel"
description: "Returns whatever the callback decides for each request. Records every request."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScriptedModel"
  hidden: true
---

<a id="E2E_ScriptedModel"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ScriptedModel : IAgentModel
```


Returns whatever the callback decides for each request. Records every request.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L164)

## Constructors

<a id="E2E_ScriptedModel__ctor_"></a>
<a id="E2E_ScriptedModel__ctor_System_Func_E2E_ModelRequest_E2E_ModelResponse__"></a>

### `ScriptedModel(Func<ModelRequest, ModelResponse>)`


```csharp
public ScriptedModel(Func<ModelRequest, ModelResponse> next)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `next` | [Func&lt;ModelRequest, ModelResponse&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L168)

## Properties

<a id="E2E_ScriptedModel_CallCount_"></a>
<a id="E2E_ScriptedModel_CallCount"></a>

### CallCount


```csharp
public int CallCount { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L176)

<a id="E2E_ScriptedModel_Name_"></a>
<a id="E2E_ScriptedModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L174)

<a id="E2E_ScriptedModel_Requests_"></a>
<a id="E2E_ScriptedModel_Requests"></a>

### Requests


```csharp
public List<ModelRequest> Requests { get; }
```


**Value:** [List&lt;ModelRequest&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L178)

## Methods

<a id="E2E_ScriptedModel_CompleteAsync_"></a>
<a id="E2E_ScriptedModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L180)
