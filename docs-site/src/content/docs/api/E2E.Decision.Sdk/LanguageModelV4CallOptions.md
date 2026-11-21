---
title: "LanguageModelV4CallOptions"
description: "The AI SDK's LanguageModelV4CallOptions (language-model-v4-call-options.ts). Upstream's abortSignal is the CancellationToken of DoGenerateAsync and DoStreamA..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4CallOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4CallOptions : IEquatable<LanguageModelV4CallOptions>
```


The AI SDK's `LanguageModelV4CallOptions` (`language-model-v4-call-options.ts`). Upstream's `abortSignal` is the
[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) of `DoGenerateAsync` and `DoStreamAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4CallOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_FrequencyPenalty_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_FrequencyPenalty"></a>

### FrequencyPenalty


```csharp
public double? FrequencyPenalty { get; init; }
```


Frequency penalty setting: the likelihood of the model repeatedly using the same words or phrases.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Headers_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string?>? Headers { get; init; }
```


Additional HTTP headers to be sent with the request. A null value leaves the header out.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_IncludeRawChunks_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_IncludeRawChunks"></a>

### IncludeRawChunks


```csharp
public bool? IncludeRawChunks { get; init; }
```


Include raw chunks in the stream. Only applicable for streaming calls.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_MaxOutputTokens_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_MaxOutputTokens"></a>

### MaxOutputTokens


```csharp
public int? MaxOutputTokens { get; init; }
```


Maximum number of tokens to generate.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_PresencePenalty_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_PresencePenalty"></a>

### PresencePenalty


```csharp
public double? PresencePenalty { get; init; }
```


Presence penalty setting: the likelihood of the model repeating information that is already in the prompt.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Prompt_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Prompt"></a>

### Prompt


```csharp
public required IReadOnlyList<LanguageModelV4Message> Prompt { get; init; }
```


A language model prompt: the standardized prompt type. This is not the user-facing prompt; the AI SDK methods map the user-facing prompt
types to this format.

**Value:** [IReadOnlyList&lt;LanguageModelV4Message&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Additional provider-specific options, passed through to the provider, keyed by provider name.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Reasoning_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Reasoning"></a>

### Reasoning


```csharp
public string? Reasoning { get; init; }
```


Reasoning effort level for the model: `provider-default`, `none`, `minimal`, `low`, `medium`, `high`,
`xhigh`, or `max`. Defaults to `provider-default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_ResponseFormat_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_ResponseFormat"></a>

### ResponseFormat


```csharp
public LanguageModelV4ResponseFormat? ResponseFormat { get; init; }
```


Response format. The output can either be text or JSON. Default is text.

**Value:** [LanguageModelV4ResponseFormat](/api/E2E.Decision.Sdk/LanguageModelV4ResponseFormat/)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Seed_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Seed"></a>

### Seed


```csharp
public int? Seed { get; init; }
```


The seed (integer) to use for random sampling. If set and supported by the model, calls generate deterministic results.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_StopSequences_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_StopSequences"></a>

### StopSequences


```csharp
public IReadOnlyList<string>? StopSequences { get; init; }
```


Stop sequences. If set, the model stops generating text when one of them is generated.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Temperature_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Temperature"></a>

### Temperature


```csharp
public double? Temperature { get; init; }
```


Temperature setting. The range depends on the provider and model.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_ToolChoice_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_ToolChoice"></a>

### ToolChoice


```csharp
public LanguageModelV4ToolChoice? ToolChoice { get; init; }
```


Specifies how the tool should be selected. Defaults to `auto`.

**Value:** [LanguageModelV4ToolChoice](/api/E2E.Decision.Sdk/LanguageModelV4ToolChoice/)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Tools_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_Tools"></a>

### Tools


```csharp
public IReadOnlyList<LanguageModelV4Tool>? Tools { get; init; }
```


The tools that are available for the model.

**Value:** [IReadOnlyList&lt;LanguageModelV4Tool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_TopK_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_TopK"></a>

### TopK


```csharp
public int? TopK { get; init; }
```


Only sample from the top K options for each subsequent token.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_TopP_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CallOptions_TopP"></a>

### TopP


```csharp
public double? TopP { get; init; }
```


Nucleus sampling.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)
