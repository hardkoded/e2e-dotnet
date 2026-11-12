---
title: "ModelToolChoice"
description: "The AI SDK's ToolChoice: auto lets the model answer in prose or call any offered tool, required makes it call some tool, and a named tool makes it call that..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelToolChoice"
  hidden: true
---

<a id="E2E_ModelToolChoice"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ModelToolChoice : IEquatable<ModelToolChoice>
```


The AI SDK's `ToolChoice`: `auto` lets the model answer in prose or call any offered tool, `required` makes it call some
tool, and a named tool makes it call that one. Each provider sends its own form of it (Anthropic's `any`, Bedrock's `any`,
OpenAI's `required`, Gemini's `ANY`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelToolChoice&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs)

## Properties

<a id="E2E_ModelToolChoice_Auto_"></a>
<a id="E2E_ModelToolChoice_Auto"></a>

### Auto


```csharp
public static ModelToolChoice Auto { get; }
```


The model decides whether to call a tool (`auto`).

**Value:** [ModelToolChoice](./)

<a id="E2E_ModelToolChoice_Forced_"></a>
<a id="E2E_ModelToolChoice_Forced"></a>

### Forced


```csharp
public bool Forced { get; }
```


Whether the choice makes the model call a tool (anything but `auto`).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_ModelToolChoice_Required_"></a>
<a id="E2E_ModelToolChoice_Required"></a>

### Required


```csharp
public static ModelToolChoice Required { get; }
```


The model must call some tool (`required`).

**Value:** [ModelToolChoice](./)

<a id="E2E_ModelToolChoice_ToolName_"></a>
<a id="E2E_ModelToolChoice_ToolName"></a>

### ToolName


```csharp
public string? ToolName { get; }
```


The tool a `tool` choice names. Null for the other two.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelToolChoice_Type_"></a>
<a id="E2E_ModelToolChoice_Type"></a>

### Type


```csharp
public string Type { get; }
```


`auto`, `required`, or `tool`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_ModelToolChoice_Tool_"></a>
<a id="E2E_ModelToolChoice_Tool_System_String_"></a>

### Tool(string)


```csharp
public static ModelToolChoice Tool(string toolName)
```


The model must call the tool named &lt;code class="paramref"&gt;toolName&lt;/code&gt; (the SDK's `{ type: 'tool', toolName }`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `toolName` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelToolChoice](./)
