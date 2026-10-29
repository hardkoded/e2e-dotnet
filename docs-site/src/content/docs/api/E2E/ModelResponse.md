---
title: "ModelResponse"
description: "ModelResponse in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelResponse"
  hidden: true
---

<a id="E2E_ModelResponse"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ModelResponse
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L113)

## Fields

<a id="E2E_ModelResponse_ContentFilter"></a>

### ContentFilter


```csharp
public const string ContentFilter = "content-filter"
```


The finish reason of a turn the provider refused (upstream's `content-filter`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L131)

## Properties

<a id="E2E_ModelResponse_Content_"></a>
<a id="E2E_ModelResponse_Content"></a>

### Content


```csharp
public string? Content { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L115)

<a id="E2E_ModelResponse_FinishReason_"></a>
<a id="E2E_ModelResponse_FinishReason"></a>

### FinishReason


```csharp
public string? FinishReason { get; init; }
```


The AI SDK finish reason that matters to the agent: [ContentFilter](#E2E_ModelResponse_ContentFilter) when the provider stopped the turn because it refused the request. Null for any other turn.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L125)

<a id="E2E_ModelResponse_RawFinishReason_"></a>
<a id="E2E_ModelResponse_RawFinishReason"></a>

### RawFinishReason


```csharp
public string? RawFinishReason { get; init; }
```


The provider's own word for the stop, such as `refusal`, when it sent one. Provider text: redact it before it reaches a message.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L128)

<a id="E2E_ModelResponse_ReasoningItems_"></a>
<a id="E2E_ModelResponse_ReasoningItems"></a>

### ReasoningItems


```csharp
public IReadOnlyList<JsonElement> ReasoningItems { get; init; }
```


The reasoning of the turn that the model can take back, to replay on the next turn. Empty when the model gave none.

**Value:** [IReadOnlyList&lt;JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L122)

<a id="E2E_ModelResponse_ToolCalls_"></a>
<a id="E2E_ModelResponse_ToolCalls"></a>

### ToolCalls


```csharp
public IReadOnlyList<ModelToolCall> ToolCalls { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelToolCall&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L117)

<a id="E2E_ModelResponse_Usage_"></a>
<a id="E2E_ModelResponse_Usage"></a>

### Usage


```csharp
public ModelUsage? Usage { get; init; }
```


**Value:** [ModelUsage](/api/E2E/ModelUsage/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L119)
