---
title: "LanguageModelV4AssistantMessage"
description: "An assistant message: text, file, custom, reasoning, reasoning file, tool call, and tool result parts."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4AssistantMessage"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4AssistantMessage"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4AssistantMessage : LanguageModelV4Message, IEquatable<LanguageModelV4Message>, IEquatable<LanguageModelV4AssistantMessage>
```


An assistant message: text, file, custom, reasoning, reasoning file, tool call, and tool result parts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4Message](/api/E2E.Decision.Sdk/LanguageModelV4Message/)

**Implements:** [IEquatable&lt;LanguageModelV4Message&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4AssistantMessage&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4AssistantMessage__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4AssistantMessage__ctor_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_LanguageModelV4PromptPart__"></a>

### `LanguageModelV4AssistantMessage(IReadOnlyList<LanguageModelV4PromptPart>)`


```csharp
public LanguageModelV4AssistantMessage(IReadOnlyList<LanguageModelV4PromptPart> Content)
```


An assistant message: text, file, custom, reasoning, reasoning file, tool call, and tool result parts.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Content` | [IReadOnlyList&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The parts. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4AssistantMessage_Content_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4AssistantMessage_Content"></a>

### Content


```csharp
public IReadOnlyList<LanguageModelV4PromptPart> Content { get; init; }
```


The parts.

**Value:** [IReadOnlyList&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
