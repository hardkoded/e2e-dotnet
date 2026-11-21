---
title: "LanguageModelV4ResponseFormat"
description: "The responseFormat of LanguageModelV4CallOptions: text, or JSON with an optional schema."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ResponseFormat"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ResponseFormat : IEquatable<LanguageModelV4ResponseFormat>
```


The `responseFormat` of [LanguageModelV4CallOptions](/api/E2E.Decision.Sdk/LanguageModelV4CallOptions/): text, or JSON with an optional schema.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4ResponseFormat&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Description_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Description"></a>

### Description


```csharp
public string? Description { get; init; }
```


Description of the output that should be generated, used by some providers for additional guidance (`json` only).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Name_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


Name of the output that should be generated, used by some providers for additional guidance (`json` only).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Schema_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Schema"></a>

### Schema


```csharp
public JsonElement? Schema { get; init; }
```


JSON schema that the generated output should conform to (`json` only).

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Text_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Text"></a>

### Text


```csharp
public static LanguageModelV4ResponseFormat Text { get; }
```


Plain text output.

**Value:** [LanguageModelV4ResponseFormat](./)

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Type_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


The format type: `text` or `json`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Json_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ResponseFormat_Json_System_Nullable_System_Text_Json_JsonElement__System_String_System_String_"></a>

### Json(JsonElement?, string?, string?)


```csharp
public static LanguageModelV4ResponseFormat Json(JsonElement? schema = null, string? name = null, string? description = null)
```


JSON output, optionally guided by a schema.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `schema` | [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `description` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [LanguageModelV4ResponseFormat](./)
