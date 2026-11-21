---
title: "OpenAIDecisionModelOptions"
description: "Settings of an OpenAIDecisionModel (the AI SDK's createOpenAI settings it reads)."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenAIDecisionModelOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class OpenAIDecisionModelOptions
```


Settings of an [OpenAIDecisionModel](/api/E2E.Decision.Sdk/OpenAIDecisionModel/) (the AI SDK's `createOpenAI` settings it reads).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/OpenAIDecisionModel.cs)

## Properties

<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_ApiKey_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


The API key. Falls back to [ApiKeyEnv](#E2E_Decision_Sdk_OpenAIDecisionModelOptions_ApiKeyEnv) when empty.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_ApiKeyEnv_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string ApiKeyEnv { get; init; }
```


The environment variable read when [ApiKey](#E2E_Decision_Sdk_OpenAIDecisionModelOptions_ApiKey) is empty. Defaults to `OPENAI_API_KEY`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_BaseUrl_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string BaseUrl { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_Headers_"></a>
<a id="E2E_Decision_Sdk_OpenAIDecisionModelOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Headers added to every request.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
