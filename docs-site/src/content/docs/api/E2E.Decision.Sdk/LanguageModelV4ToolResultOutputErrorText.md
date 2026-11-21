---
title: "LanguageModelV4ToolResultOutputErrorText"
description: "A tool error as text."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutputErrorText"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorText"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultOutputErrorText : LanguageModelV4ToolResultOutput, IEquatable<LanguageModelV4ToolResultOutput>, IEquatable<LanguageModelV4ToolResultOutputErrorText>
```


A tool error as text.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultOutputErrorText&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorText__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorText__ctor_System_String_"></a>

### LanguageModelV4ToolResultOutputErrorText(string)


```csharp
public LanguageModelV4ToolResultOutputErrorText(string Value)
```


A tool error as text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The text. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorText_Value_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorText_Value"></a>

### Value


```csharp
public string Value { get; init; }
```


The text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
