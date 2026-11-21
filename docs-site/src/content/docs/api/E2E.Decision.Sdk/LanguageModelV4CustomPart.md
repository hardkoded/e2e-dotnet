---
title: "LanguageModelV4CustomPart"
description: "Provider-specific content part of a prompt, with no standardized payload beyond provider options."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4CustomPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4CustomPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4CustomPart : LanguageModelV4PromptPart, IEquatable<LanguageModelV4PromptPart>, IEquatable<LanguageModelV4CustomPart>
```


Provider-specific content part of a prompt, with no standardized payload beyond provider options.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4PromptPart](/api/E2E.Decision.Sdk/LanguageModelV4PromptPart/)

**Implements:** [IEquatable&lt;LanguageModelV4PromptPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4CustomPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4CustomPart__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CustomPart__ctor_System_String_"></a>

### LanguageModelV4CustomPart(string)


```csharp
public LanguageModelV4CustomPart(string Kind)
```


Provider-specific content part of a prompt, with no standardized payload beyond provider options.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Kind` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The kind of custom content, in the format `{provider}.{provider-type}`. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4CustomPart_Kind_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4CustomPart_Kind"></a>

### Kind


```csharp
public string Kind { get; init; }
```


The kind of custom content, in the format `{provider}.{provider-type}`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
