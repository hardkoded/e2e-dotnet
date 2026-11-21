---
title: "LanguageModelV4SystemMessage"
description: "A system message."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4SystemMessage"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4SystemMessage"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4SystemMessage : LanguageModelV4Message, IEquatable<LanguageModelV4Message>, IEquatable<LanguageModelV4SystemMessage>
```


A system message.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4Message](/api/E2E.Decision.Sdk/LanguageModelV4Message/)

**Implements:** [IEquatable&lt;LanguageModelV4Message&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4SystemMessage&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4SystemMessage__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4SystemMessage__ctor_System_String_"></a>

### LanguageModelV4SystemMessage(string)


```csharp
public LanguageModelV4SystemMessage(string Content)
```


A system message.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Content` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The instructions. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4SystemMessage_Content_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4SystemMessage_Content"></a>

### Content


```csharp
public string Content { get; init; }
```


The instructions.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
