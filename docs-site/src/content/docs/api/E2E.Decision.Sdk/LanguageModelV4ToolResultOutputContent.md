---
title: "LanguageModelV4ToolResultOutputContent"
description: "Tool output made of content parts: text, files, and custom content."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutputContent"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputContent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultOutputContent : LanguageModelV4ToolResultOutput, IEquatable<LanguageModelV4ToolResultOutput>, IEquatable<LanguageModelV4ToolResultOutputContent>
```


Tool output made of content parts: text, files, and custom content.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultOutputContent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputContent__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputContent__ctor_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_LanguageModelV4ToolResultContentPart__"></a>

### `LanguageModelV4ToolResultOutputContent(IReadOnlyList<LanguageModelV4ToolResultContentPart>)`


```csharp
public LanguageModelV4ToolResultOutputContent(IReadOnlyList<LanguageModelV4ToolResultContentPart> Value)
```


Tool output made of content parts: text, files, and custom content.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [IReadOnlyList&lt;LanguageModelV4ToolResultContentPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The content parts. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputContent_Value_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputContent_Value"></a>

### Value


```csharp
public IReadOnlyList<LanguageModelV4ToolResultContentPart> Value { get; init; }
```


The content parts.

**Value:** [IReadOnlyList&lt;LanguageModelV4ToolResultContentPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
