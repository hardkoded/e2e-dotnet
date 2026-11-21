---
title: "LanguageModelV4ToolResultOutput"
description: "The AI SDK's LanguageModelV4ToolResultOutput: the result of a tool call. One of LanguageModelV4ToolResultOutputText, LanguageModelV4ToolResultOutputJson, Lan..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutput"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutput"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public abstract record LanguageModelV4ToolResultOutput : IEquatable<LanguageModelV4ToolResultOutput>
```


The AI SDK's `LanguageModelV4ToolResultOutput`: the result of a tool call. One of [LanguageModelV4ToolResultOutputText](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputText/),
[LanguageModelV4ToolResultOutputJson](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputJson/), [LanguageModelV4ToolResultOutputExecutionDenied](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputExecutionDenied/),
[LanguageModelV4ToolResultOutputErrorText](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputErrorText/), [LanguageModelV4ToolResultOutputErrorJson](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputErrorJson/), or
[LanguageModelV4ToolResultOutputContent](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputContent/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [LanguageModelV4ToolResultOutputContent](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputContent/), [LanguageModelV4ToolResultOutputErrorJson](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputErrorJson/), [LanguageModelV4ToolResultOutputErrorText](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputErrorText/), [LanguageModelV4ToolResultOutputExecutionDenied](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputExecutionDenied/), [LanguageModelV4ToolResultOutputJson](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputJson/), [LanguageModelV4ToolResultOutputText](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutputText/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutput_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutput_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Provider-specific options.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
