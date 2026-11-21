---
title: "LanguageModelV4ReasoningPart"
description: "Reasoning content part of a prompt."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ReasoningPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ReasoningPart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4ReasoningPart>
```


Reasoning content part of a prompt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ReasoningPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningPart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningPart__ctor_System_String_"></a>

### LanguageModelV4ReasoningPart(string)


```csharp
public LanguageModelV4ReasoningPart(string Text)
```


Reasoning content part of a prompt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The reasoning text. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningPart_Text_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningPart_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


The reasoning text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
