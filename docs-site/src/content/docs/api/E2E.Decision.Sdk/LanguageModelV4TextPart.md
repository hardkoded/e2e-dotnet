---
title: "LanguageModelV4TextPart"
description: "Text content part of a prompt."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4TextPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4TextPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4TextPart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4TextPart>
```


Text content part of a prompt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4TextPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4TextPart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4TextPart__ctor_System_String_"></a>

### LanguageModelV4TextPart(string)


```csharp
public LanguageModelV4TextPart(string Text)
```


Text content part of a prompt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The text content. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4TextPart_Text_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4TextPart_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


The text content.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
