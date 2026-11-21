---
title: "LanguageModelV4ProviderTool"
description: "A tool that is built into a provider (LanguageModelV4ProviderTool)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ProviderTool"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ProviderTool : LanguageModelV4Tool, IEquatable<LanguageModelV4Tool>, IEquatable<LanguageModelV4ProviderTool>
```


A tool that is built into a provider (`LanguageModelV4ProviderTool`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LanguageModelV4Tool](/api/E2E.Decision.Sdk/LanguageModelV4Tool/)

**Implements:** [IEquatable&lt;LanguageModelV4Tool&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LanguageModelV4ProviderTool&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Constructors

<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool__ctor_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool__ctor_System_String_System_String_System_Text_Json_JsonElement_"></a>

### LanguageModelV4ProviderTool(string, string, JsonElement)


```csharp
public LanguageModelV4ProviderTool(string Id, string Name, JsonElement Args)
```


A tool that is built into a provider (`LanguageModelV4ProviderTool`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The ID of the tool, in the format `{provider}.{unique-tool-name}`. |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The name of the tool, unique within a call. |
| `Args` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) | The arguments of the tool, a JSON object. |

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool_Args_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool_Args"></a>

### Args


```csharp
public JsonElement Args { get; init; }
```


The arguments of the tool, a JSON object.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool_Id_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


The ID of the tool, in the format `{provider}.{unique-tool-name}`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool_Name_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ProviderTool_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


The name of the tool, unique within a call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
