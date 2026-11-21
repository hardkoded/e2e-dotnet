---
title: "LanguageModelV4ToolApprovalResponsePart"
description: "Tool approval response content part of a prompt: the user's decision to approve or deny a provider-executed tool call."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolApprovalResponsePart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolApprovalResponsePart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4ToolApprovalResponsePart>
```


Tool approval response content part of a prompt: the user's decision to approve or deny a provider-executed tool call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolApprovalResponsePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart__ctor_System_String_System_Boolean_"></a>

### LanguageModelV4ToolApprovalResponsePart(string, bool)


```csharp
public LanguageModelV4ToolApprovalResponsePart(string ApprovalId, bool Approved)
```


Tool approval response content part of a prompt: the user's decision to approve or deny a provider-executed tool call.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ApprovalId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The ID of the approval request this response refers to. |
| `Approved` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) | Whether the approval was granted. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart_ApprovalId_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart_ApprovalId"></a>

### ApprovalId


```csharp
public string ApprovalId { get; init; }
```


The ID of the approval request this response refers to.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart_Approved_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart_Approved"></a>

### Approved


```csharp
public bool Approved { get; init; }
```


Whether the approval was granted.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart_Reason_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolApprovalResponsePart_Reason"></a>

### Reason


```csharp
public string? Reason { get; init; }
```


The reason for the approval or denial.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
