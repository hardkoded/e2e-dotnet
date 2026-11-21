---
title: "LanguageModelV4ToolResultOutputExecutionDenied"
description: "The user denied the execution of the tool call."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutputExecutionDenied"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputExecutionDenied"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultOutputExecutionDenied : LanguageModelV4ToolResultOutput, IEquatable<LanguageModelV4ToolResultOutput>, IEquatable<LanguageModelV4ToolResultOutputExecutionDenied>
```


The user denied the execution of the tool call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultOutputExecutionDenied&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputExecutionDenied_Reason_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputExecutionDenied_Reason"></a>

### Reason


```csharp
public string? Reason { get; init; }
```


The reason for the denial.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
