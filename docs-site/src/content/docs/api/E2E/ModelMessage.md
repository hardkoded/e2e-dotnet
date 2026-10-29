---
title: "ModelMessage"
description: "ModelMessage in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelMessage"
  hidden: true
---

<a id="E2E_ModelMessage"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ModelMessage
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L43)

## Properties

<a id="E2E_ModelMessage_Content_"></a>
<a id="E2E_ModelMessage_Content"></a>

### Content


```csharp
public string? Content { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L47)

<a id="E2E_ModelMessage_Images_"></a>
<a id="E2E_ModelMessage_Images"></a>

### Images


```csharp
public IReadOnlyList<ModelImage>? Images { get; init; }
```


The images that go with a `user` turn, after its text. A provider sends them as the turn's image parts; on any other role they are not sent.

**Value:** [IReadOnlyList&lt;ModelImage&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L59)

<a id="E2E_ModelMessage_Name_"></a>
<a id="E2E_ModelMessage_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L53)

<a id="E2E_ModelMessage_ReasoningItems_"></a>
<a id="E2E_ModelMessage_ReasoningItems"></a>

### ReasoningItems


```csharp
public IReadOnlyList<JsonElement>? ReasoningItems { get; init; }
```


The reasoning the model returned with this turn, sent back as it came: encrypted items for [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/), signed thinking blocks for [AnthropicModel](/api/E2E/AnthropicModel/), thought parts for [GoogleModel](/api/E2E/GoogleModel/), the `reasoningContent` blocks for [BedrockModel](/api/E2E/BedrockModel/), the reasoning text as a JSON string for [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/).

**Value:** [IReadOnlyList&lt;JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L56)

<a id="E2E_ModelMessage_Role_"></a>
<a id="E2E_ModelMessage_Role"></a>

### Role


```csharp
public required string Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L45)

<a id="E2E_ModelMessage_ToolCallId_"></a>
<a id="E2E_ModelMessage_ToolCallId"></a>

### ToolCallId


```csharp
public string? ToolCallId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L51)

<a id="E2E_ModelMessage_ToolCalls_"></a>
<a id="E2E_ModelMessage_ToolCalls"></a>

### ToolCalls


```csharp
public IReadOnlyList<ModelToolCall>? ToolCalls { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelToolCall&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L49)
