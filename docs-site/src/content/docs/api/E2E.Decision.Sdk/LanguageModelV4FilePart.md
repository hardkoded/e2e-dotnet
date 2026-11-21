---
title: "LanguageModelV4FilePart"
description: "File content part of a prompt."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4FilePart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4FilePart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4FilePart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4FilePart>
```


File content part of a prompt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4FilePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4FilePart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FilePart__ctor_E2E_Decision_Sdk_SharedV4FileData_System_String_"></a>

### LanguageModelV4FilePart(SharedV4FileData, string)


```csharp
public LanguageModelV4FilePart(SharedV4FileData Data, string MediaType)
```


File content part of a prompt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/) | The file data. |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Either a full IANA media type (`image/png`) or just the top-level segment (`image`). |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4FilePart_Data_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FilePart_Data"></a>

### Data


```csharp
public SharedV4FileData Data { get; init; }
```


The file data.

**Value:** [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

<a id="E2E_Decision_Sdk_LanguageModelV4FilePart_Filename_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FilePart_Filename"></a>

### Filename


```csharp
public string? Filename { get; init; }
```


Optional filename of the file.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4FilePart_MediaType_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FilePart_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


Either a full IANA media type (`image/png`) or just the top-level segment (`image`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
