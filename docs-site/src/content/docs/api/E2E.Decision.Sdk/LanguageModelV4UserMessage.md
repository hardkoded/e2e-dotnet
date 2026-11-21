---
title: "LanguageModelV4UserMessage"
description: "A user message: text and file parts."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4UserMessage"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4UserMessage"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4UserMessage : LanguageModelV4Message, IEquatable<LanguageModelV4Message>, IEquatable<LanguageModelV4UserMessage>
```


A user message: text and file parts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4Message](/api/E2E.Decision.Sdk/LanguageModelV4Message/)

**Implements:** [IEquatable&lt;LanguageModelV4Message&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4UserMessage&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4UserMessage__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4UserMessage__ctor_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_LanguageModelV4PromptPart__"></a>

### `LanguageModelV4UserMessage(IReadOnlyList<LanguageModelV4PromptPart>)`


```csharp
public LanguageModelV4UserMessage(IReadOnlyList<LanguageModelV4PromptPart> Content)
```


A user message: text and file parts.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Content` | [IReadOnlyList&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The [LanguageModelV4TextPart](/api/E2E.Decision.Sdk/LanguageModelV4TextPart/) and [LanguageModelV4FilePart](/api/E2E.Decision.Sdk/LanguageModelV4FilePart/) parts. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4UserMessage_Content_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4UserMessage_Content"></a>

### Content


```csharp
public IReadOnlyList<LanguageModelV4PromptPart> Content { get; init; }
```


The [LanguageModelV4TextPart](/api/E2E.Decision.Sdk/LanguageModelV4TextPart/) and [LanguageModelV4FilePart](/api/E2E.Decision.Sdk/LanguageModelV4FilePart/) parts.

**Value:** [IReadOnlyList&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
