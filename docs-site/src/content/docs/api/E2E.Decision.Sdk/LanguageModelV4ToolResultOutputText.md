---
title: "LanguageModelV4ToolResultOutputText"
description: "Text tool output that is sent directly to the API."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutputText"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputText"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultOutputText : LanguageModelV4ToolResultOutput, IEquatable<LanguageModelV4ToolResultOutput>, IEquatable<LanguageModelV4ToolResultOutputText>
```


Text tool output that is sent directly to the API.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultOutputText&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputText__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputText__ctor_System_String_"></a>

### LanguageModelV4ToolResultOutputText(string)


```csharp
public LanguageModelV4ToolResultOutputText(string Value)
```


Text tool output that is sent directly to the API.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The text. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputText_Value_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputText_Value"></a>

### Value


```csharp
public string Value { get; init; }
```


The text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
