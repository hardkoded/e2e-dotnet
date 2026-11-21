---
title: "LanguageModelV4Message"
description: "The AI SDK's LanguageModelV4Message (language-model/v4/language-model-v4-prompt.ts). A prompt (LanguageModelV4Prompt) is a list of messages. This is not the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4Message"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4Message"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public abstract record LanguageModelV4Message : IEquatable<LanguageModelV4Message>
```


The AI SDK's `LanguageModelV4Message` (`language-model/v4/language-model-v4-prompt.ts`). A prompt (`LanguageModelV4Prompt`)
is a list of messages. This is not the user-facing prompt: it is the standardized format a language model takes.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4Message&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [LanguageModelV4AssistantMessage](/api/E2E.Decision.Sdk/LanguageModelV4AssistantMessage/), [LanguageModelV4SystemMessage](/api/E2E.Decision.Sdk/LanguageModelV4SystemMessage/), [LanguageModelV4ToolMessage](/api/E2E.Decision.Sdk/LanguageModelV4ToolMessage/), [LanguageModelV4UserMessage](/api/E2E.Decision.Sdk/LanguageModelV4UserMessage/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4Message_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4Message_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Additional provider-specific options, passed through to the provider, keyed by provider name.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
