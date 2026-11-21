---
title: "LanguageModelV4ToolResultPart"
description: "Tool result content part of a prompt: the result of the tool call with the matching ID."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultPart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4ToolResultPart>
```


Tool result content part of a prompt: the result of the tool call with the matching ID.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart__ctor_System_String_System_String_E2E_Decision_Sdk_LanguageModelV4ToolResultOutput_"></a>

### LanguageModelV4ToolResultPart(string, string, LanguageModelV4ToolResultOutput)


```csharp
public LanguageModelV4ToolResultPart(string ToolCallId, string ToolName, LanguageModelV4ToolResultOutput Output)
```


Tool result content part of a prompt: the result of the tool call with the matching ID.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ToolCallId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The ID of the tool call this result belongs to. |
| `ToolName` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The name of the tool that generated the result. |
| `Output` | [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/) | The result of the tool call. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart_Output_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart_Output"></a>

### Output


```csharp
public LanguageModelV4ToolResultOutput Output { get; init; }
```


The result of the tool call.

**Value:** [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart_ToolCallId_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart_ToolCallId"></a>

### ToolCallId


```csharp
public string ToolCallId { get; init; }
```


The ID of the tool call this result belongs to.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart_ToolName_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultPart_ToolName"></a>

### ToolName


```csharp
public string ToolName { get; init; }
```


The name of the tool that generated the result.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
