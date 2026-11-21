---
title: "LanguageModelV4ToolResultOutputJson"
description: "JSON tool output."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutputJson"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputJson"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultOutputJson : LanguageModelV4ToolResultOutput, IEquatable<LanguageModelV4ToolResultOutput>, IEquatable<LanguageModelV4ToolResultOutputJson>
```


JSON tool output.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultOutputJson&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputJson__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputJson__ctor_System_Text_Json_JsonElement_"></a>

### LanguageModelV4ToolResultOutputJson(JsonElement)


```csharp
public LanguageModelV4ToolResultOutputJson(JsonElement Value)
```


JSON tool output.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) | The JSON value. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputJson_Value_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputJson_Value"></a>

### Value


```csharp
public JsonElement Value { get; init; }
```


The JSON value.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)
