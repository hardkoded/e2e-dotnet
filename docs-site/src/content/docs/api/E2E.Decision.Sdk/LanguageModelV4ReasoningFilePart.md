---
title: "LanguageModelV4ReasoningFilePart"
description: "Reasoning file content part of a prompt: a file generated as part of reasoning."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ReasoningFilePart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ReasoningFilePart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4ReasoningFilePart>
```


Reasoning file content part of a prompt: a file generated as part of reasoning.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ReasoningFilePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart__ctor_E2E_Decision_Sdk_SharedV4FileData_System_String_"></a>

### LanguageModelV4ReasoningFilePart(SharedV4FileData, string)


```csharp
public LanguageModelV4ReasoningFilePart(SharedV4FileData Data, string MediaType)
```


Reasoning file content part of a prompt: a file generated as part of reasoning.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/) | The file data: a [SharedV4FileDataData](/api/E2E.Decision.Sdk/SharedV4FileDataData/) or a [SharedV4FileDataUrl](/api/E2E.Decision.Sdk/SharedV4FileDataUrl/). |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The IANA media type of the file. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart_Data_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart_Data"></a>

### Data


```csharp
public SharedV4FileData Data { get; init; }
```


The file data: a [SharedV4FileDataData](/api/E2E.Decision.Sdk/SharedV4FileDataData/) or a [SharedV4FileDataUrl](/api/E2E.Decision.Sdk/SharedV4FileDataUrl/).

**Value:** [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart_MediaType_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ReasoningFilePart_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


The IANA media type of the file.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
