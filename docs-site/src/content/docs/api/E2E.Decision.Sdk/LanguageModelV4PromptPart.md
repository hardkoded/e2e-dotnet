---
title: "LanguageModelV4PromptPart"
description: "A content part of a prompt message."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4PromptPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4PromptPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public abstract record LanguageModelV4PromptPart : IEquatable<LanguageModelV4PromptPart>
```


A content part of a prompt message.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [LanguageModelV4CustomPart](/api/E2E.Decision.Sdk/LanguageModelV4CustomPart/), [LanguageModelV4FilePart](/api/E2E.Decision.Sdk/LanguageModelV4FilePart/), [LanguageModelV4ReasoningFilePart](/api/E2E.Decision.Sdk/LanguageModelV4ReasoningFilePart/), [LanguageModelV4ReasoningPart](/api/E2E.Decision.Sdk/LanguageModelV4ReasoningPart/), [LanguageModelV4TextPart](/api/E2E.Decision.Sdk/LanguageModelV4TextPart/), [LanguageModelV4ToolApprovalResponsePart](/api/E2E.Decision.Sdk/LanguageModelV4ToolApprovalResponsePart/), [LanguageModelV4ToolCallPart](/api/E2E.Decision.Sdk/LanguageModelV4ToolCallPart/), [LanguageModelV4ToolResultPart](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultPart/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4PromptPart_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4PromptPart_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Additional provider-specific options, passed through to the provider, keyed by provider name.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
