---
title: "LanguageModelV4ToolResultContentText"
description: "Text content."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultContentText"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentText"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultContentText : LanguageModelV4ToolResultContentPart, IEquatable<LanguageModelV4ToolResultContentPart>, IEquatable<LanguageModelV4ToolResultContentText>
```


Text content.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultContentPart](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultContentPart/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultContentPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultContentText&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentText__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentText__ctor_System_String_"></a>

### LanguageModelV4ToolResultContentText(string)


```csharp
public LanguageModelV4ToolResultContentText(string Text)
```


Text content.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The text. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentText_Text_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultContentText_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


The text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
