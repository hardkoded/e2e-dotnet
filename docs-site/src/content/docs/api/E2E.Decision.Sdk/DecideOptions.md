---
title: "DecideOptions"
description: "What RunAsync takes (the arguments of the AI SDK's experimental_decide)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecideOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecideOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecideOptions
```


What `RunAsync` takes (the arguments of the AI SDK's `experimental_decide`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ExperimentalDecide.cs)

## Properties

<a id="E2E_Decision_Sdk_DecideOptions_EvaluationModel_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_EvaluationModel"></a>

### EvaluationModel


```csharp
public IEvaluationModel? EvaluationModel { get; init; }
```


Deprecated: use [Model](#E2E_Decision_Sdk_DecideOptions_Model). A deprecated evaluation model (`doEvaluate`).

**Value:** [IEvaluationModel](/api/E2E.Decision.Sdk/IEvaluationModel/)

<a id="E2E_Decision_Sdk_DecideOptions_ExperimentalDownload_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_ExperimentalDownload"></a>

### ExperimentalDownload


```csharp
public DownloadFunction? ExperimentalDownload { get; init; }
```


The download function URL file state needs (`experimental_download`). Use [Create(CreateDownloadOptions)](/api/E2E.Decision.Sdk/CreateDownload/#E2E_Decision_Sdk_CreateDownload_Create_E2E_Decision_Sdk_CreateDownloadOptions_) with a transport that
validates DNS at connection time for untrusted URLs. The model's own HTTP client is not used.

**Value:** [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/)

<a id="E2E_Decision_Sdk_DecideOptions_Headers_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideOptions_MaxOutputTokens_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_MaxOutputTokens"></a>

### MaxOutputTokens


```csharp
public int? MaxOutputTokens { get; init; }
```


The most output tokens, for models that support it. At least 1.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecideOptions_MaxRetries_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_MaxRetries"></a>

### MaxRetries


```csharp
public int? MaxRetries { get; init; }
```


Retries of a call that fails with a retryable error. Null is the AI SDK's default, 2.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecideOptions_Model_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_Model"></a>

### Model


```csharp
public IDecisionModel? Model { get; init; }
```


The model that decides. Set exactly one of [Model](#E2E_Decision_Sdk_DecideOptions_Model), [EvaluationModel](#E2E_Decision_Sdk_DecideOptions_EvaluationModel), and [ModelId](#E2E_Decision_Sdk_DecideOptions_ModelId).

**Value:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_Sdk_DecideOptions_ModelId_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_ModelId"></a>

### ModelId


```csharp
public string? ModelId { get; init; }
```


A model id, resolved through [AiSdkDefaultProvider](/api/E2E.Decision.Sdk/ResolveModel/#E2E_Decision_Sdk_ResolveModel_AiSdkDefaultProvider) (the Gateway when none is set).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideOptions_OnEnd_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_OnEnd"></a>

### OnEnd


```csharp
public Func<DecideEndEvent, Task>? OnEnd { get; init; }
```


Called when the decide operation completes.

**Value:** [Func&lt;DecideEndEvent, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Decision_Sdk_DecideOptions_OnStart_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_OnStart"></a>

### OnStart


```csharp
public Func<DecideStartEvent, Task>? OnStart { get; init; }
```


Called when the decide operation begins.

**Value:** [Func&lt;DecideStartEvent, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Decision_Sdk_DecideOptions_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The provider options, keyed by provider name.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideOptions_Questions_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_Questions"></a>

### Questions


```csharp
public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, DecisionQuestion&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideOptions_Reasoning_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_Reasoning"></a>

### Reasoning


```csharp
public string? Reasoning { get; init; }
```


Reasoning effort, for models that support it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecideOptions_RuntimeContext_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_RuntimeContext"></a>

### RuntimeContext


```csharp
public IReadOnlyDictionary<string, object?>? RuntimeContext { get; init; }
```


User-defined runtime context. Treat runtime context as immutable.

**Value:** [IReadOnlyDictionary&lt;string, object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecideOptions_State_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_State"></a>

### State


```csharp
public required IReadOnlyList<DecisionStatePart> State { get; init; }
```


**Value:** [IReadOnlyList&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_DecideOptions_Telemetry_"></a>
<a id="E2E_Decision_Sdk_DecideOptions_Telemetry"></a>

### Telemetry


```csharp
public TelemetryOptions? Telemetry { get; init; }
```


Optional telemetry configuration.

**Value:** [TelemetryOptions](/api/E2E.Decision.Sdk/TelemetryOptions/)
