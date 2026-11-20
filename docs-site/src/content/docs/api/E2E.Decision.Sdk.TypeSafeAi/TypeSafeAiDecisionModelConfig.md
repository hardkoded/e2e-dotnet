---
title: "TypeSafeAiDecisionModelConfig"
description: "The config of a DecisionTypeSafeAiModel (upstream's TypeSafeAiDecisionModelConfig)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeSafeAiDecisionModelConfig"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed class TypeSafeAiDecisionModelConfig
```


The config of a [DecisionTypeSafeAiModel](/api/E2E.Decision.Sdk.TypeSafeAi/DecisionTypeSafeAiModel/) (upstream's `TypeSafeAiDecisionModelConfig`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiDecisionModel.cs)

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_BaseUrl_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_BaseUrl"></a>

### BaseUrl


```csharp
public required string BaseUrl { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_Headers_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_Headers"></a>

### Headers


```csharp
public Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>? Headers { get; init; }
```


Resolves the headers sent with every request, such as `Authorization`. Without it, the model sends the `TYPESAFE_AI_API_KEY`
environment variable as the bearer, read at each call.

**Value:** [Func&lt;CancellationToken, Task&lt;IReadOnlyDictionary&lt;string, string&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_HttpClient_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_HttpClient"></a>

### HttpClient


```csharp
public HttpClient? HttpClient { get; init; }
```


The client that sends the requests. Defaults to a client of the model's own, with a 120 second timeout.

**Value:** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_Provider_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelConfig_Provider"></a>

### Provider


```csharp
public required string Provider { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
