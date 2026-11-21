---
title: "ILanguageModelV4"
description: "The AI SDK's LanguageModelV4 (language-model/v4/language-model-v4.ts): a language model that implements the language model interface version 4."
editUrl: false
prev: false
next: false
sidebar:
  label: "ILanguageModelV4"
  hidden: true
---

<a id="E2E_Decision_Sdk_ILanguageModelV4"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface ILanguageModelV4
```


The AI SDK's `LanguageModelV4` (`language-model/v4/language-model-v4.ts`): a language model that implements the language model
interface version 4.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_ILanguageModelV4_ModelId_"></a>
<a id="E2E_Decision_Sdk_ILanguageModelV4_ModelId"></a>

### ModelId


```csharp
string ModelId { get; }
```


The provider-specific model ID.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ILanguageModelV4_Provider_"></a>
<a id="E2E_Decision_Sdk_ILanguageModelV4_Provider"></a>

### Provider


```csharp
string Provider { get; }
```


The provider ID.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ILanguageModelV4_SpecificationVersion_"></a>
<a id="E2E_Decision_Sdk_ILanguageModelV4_SpecificationVersion"></a>

### SpecificationVersion


```csharp
string SpecificationVersion { get; }
```


The language model interface version the model implements: `v4`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ILanguageModelV4_SupportedUrls_"></a>
<a id="E2E_Decision_Sdk_ILanguageModelV4_SupportedUrls"></a>

### SupportedUrls


```csharp
IReadOnlyDictionary<string, IReadOnlyList<Regex>> SupportedUrls { get; }
```


Supported URL patterns by media type: the keys are media type patterns or full media types (`*/*`, `audio/*`,
`application/pdf`) and the values are the patterns that match the lower-case URLs. A matched URL is supported natively by the
model and is not downloaded.

**Value:** [IReadOnlyDictionary&lt;string, IReadOnlyList&lt;Regex&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

## Methods

<a id="E2E_Decision_Sdk_ILanguageModelV4_DoGenerateAsync_"></a>
<a id="E2E_Decision_Sdk_ILanguageModelV4_DoGenerateAsync_E2E_Decision_Sdk_LanguageModelV4CallOptions_System_Threading_CancellationToken_"></a>

### DoGenerateAsync(LanguageModelV4CallOptions, CancellationToken)


```csharp
Task<LanguageModelV4GenerateResult> DoGenerateAsync(LanguageModelV4CallOptions options, CancellationToken cancellationToken = default)
```


Generates a language model output (non-streaming).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [LanguageModelV4CallOptions](/api/E2E.Decision.Sdk/LanguageModelV4CallOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;LanguageModelV4GenerateResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_ILanguageModelV4_DoStreamAsync_"></a>
<a id="E2E_Decision_Sdk_ILanguageModelV4_DoStreamAsync_E2E_Decision_Sdk_LanguageModelV4CallOptions_System_Threading_CancellationToken_"></a>

### DoStreamAsync(LanguageModelV4CallOptions, CancellationToken)


```csharp
Task<LanguageModelV4StreamResult> DoStreamAsync(LanguageModelV4CallOptions options, CancellationToken cancellationToken = default)
```


Generates a language model output (streaming).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [LanguageModelV4CallOptions](/api/E2E.Decision.Sdk/LanguageModelV4CallOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;LanguageModelV4StreamResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
