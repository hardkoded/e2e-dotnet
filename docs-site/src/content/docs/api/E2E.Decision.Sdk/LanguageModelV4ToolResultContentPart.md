---
title: "LanguageModelV4ToolResultContentPart"
description: "A part of a LanguageModelV4ToolResultOutputContent."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultContentPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public abstract record LanguageModelV4ToolResultContentPart : IEquatable<LanguageModelV4ToolResultContentPart>
```


A part of a [LanguageModelV4ToolResultOutputContent](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputContent/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultContentPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [LanguageModelV4ToolResultContentCustom](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultContentCustom/), [LanguageModelV4ToolResultContentFile](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultContentFile/), [LanguageModelV4ToolResultContentText](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultContentText/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentPart_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentPart_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Provider-specific options.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
