---
title: "LanguageModelV4ToolChoice"
description: "How the tool is selected (LanguageModelV4ToolChoice): automatic, none, required, or a specific tool."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4ToolChoice"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LanguageModelV4ToolChoice : IEquatable<LanguageModelV4ToolChoice>
```


How the tool is selected (`LanguageModelV4ToolChoice`): automatic, none, required, or a specific tool.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LanguageModelV4ToolChoice&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Auto_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Auto"></a>

### Auto


```csharp
public static LanguageModelV4ToolChoice Auto { get; }
```


The tool selection is automatic; the model may call no tool.

**Value:** [LanguageModelV4ToolChoice](./)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_None_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_None"></a>

### None


```csharp
public static LanguageModelV4ToolChoice None { get; }
```


No tool must be selected.

**Value:** [LanguageModelV4ToolChoice](./)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Required_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Required"></a>

### Required


```csharp
public static LanguageModelV4ToolChoice Required { get; }
```


One of the available tools must be selected.

**Value:** [LanguageModelV4ToolChoice](./)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_ToolName_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_ToolName"></a>

### ToolName


```csharp
public string? ToolName { get; init; }
```


The name of the tool that must be selected (`tool` only).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Type_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


The choice type: `auto`, `none`, `required`, or `tool`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Tool_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4ToolChoice_Tool_System_String_"></a>

### Tool(string)


```csharp
public static LanguageModelV4ToolChoice Tool(string toolName)
```


A specific tool must be selected.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `toolName` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [LanguageModelV4ToolChoice](./)
