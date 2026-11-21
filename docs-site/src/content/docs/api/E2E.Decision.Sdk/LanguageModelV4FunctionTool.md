---
title: "LanguageModelV4FunctionTool"
description: "A tool that has a name, a description, and a JSON schema for its input (LanguageModelV4FunctionTool)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4FunctionTool"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4FunctionTool : LanguageModelV4Tool, IEquatable<LanguageModelV4Tool>, IEquatable<LanguageModelV4FunctionTool>
```


A tool that has a name, a description, and a JSON schema for its input (`LanguageModelV4FunctionTool`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4Tool](/api/E2E.Decision.Sdk/LanguageModelV4Tool/)

**Implements:** [IEquatable&lt;LanguageModelV4Tool&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4FunctionTool&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool__ctor_System_String_System_Text_Json_JsonElement_"></a>

### LanguageModelV4FunctionTool(string, JsonElement)


```csharp
public LanguageModelV4FunctionTool(string Name, JsonElement InputSchema)
```


A tool that has a name, a description, and a JSON schema for its input (`LanguageModelV4FunctionTool`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The name of the tool, unique within a call. |
| `InputSchema` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) | The JSON schema of the tool's input. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_Description_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_Description"></a>

### Description


```csharp
public string? Description { get; init; }
```


A description of the tool, which the model uses to decide when and how to call it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_InputExamples_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_InputExamples"></a>

### InputExamples


```csharp
public IReadOnlyList<JsonElement>? InputExamples { get; init; }
```


Example inputs of the tool, each a JSON object.

**Value:** [IReadOnlyList&lt;JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_InputSchema_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_InputSchema"></a>

### InputSchema


```csharp
public JsonElement InputSchema { get; init; }
```


The JSON schema of the tool's input.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_Name_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


The name of the tool, unique within a call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Additional provider-specific options, keyed by provider name.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_Strict_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4FunctionTool_Strict"></a>

### Strict


```csharp
public bool? Strict { get; init; }
```


The strict mode setting, for providers that support it.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)
