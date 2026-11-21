---
title: "LanguageModelV4ToolResultOutputErrorJson"
description: "A tool error as JSON."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolResultOutputErrorJson"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorJson"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolResultOutputErrorJson : LanguageModelV4ToolResultOutput, IEquatable<LanguageModelV4ToolResultOutput>, IEquatable<LanguageModelV4ToolResultOutputErrorJson>
```


A tool error as JSON.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4ToolResultOutput](/api/E2E.Decision.Sdk/LanguageModelV4ToolResultOutput/)

**Implements:** [IEquatable&lt;LanguageModelV4ToolResultOutput&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ToolResultOutputErrorJson&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4Prompt.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorJson__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorJson__ctor_System_Text_Json_JsonElement_"></a>

### LanguageModelV4ToolResultOutputErrorJson(JsonElement)


```csharp
public LanguageModelV4ToolResultOutputErrorJson(JsonElement Value)
```


A tool error as JSON.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) | The JSON value. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorJson_Value_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolResultOutputErrorJson_Value"></a>

### Value


```csharp
public JsonElement Value { get; init; }
```


The JSON value.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)
