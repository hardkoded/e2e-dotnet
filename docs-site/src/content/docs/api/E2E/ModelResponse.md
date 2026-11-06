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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs)

## Fields

<a id="E2E_ModelResponse_ContentFilter"></a>

### ContentFilter


```csharp
public const string ContentFilter = "content-filter"
```


The finish reason of a turn the provider refused (upstream's `content-filter`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelResponse_ErrorFinish"></a>

### ErrorFinish


```csharp
public const string ErrorFinish = "error"
```


The finish reason of a turn the provider ended on an error (upstream's `error`), whatever text came with it. No built-in provider reports it; a custom [IAgentModel](/api/E2E/IAgentModel/) may.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

<a id="E2E_ModelResponse_Content_"></a>
<a id="E2E_ModelResponse_Content"></a>

### Content


```csharp
public string? Content { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelResponse_FinishReason_"></a>
<a id="E2E_ModelResponse_FinishReason"></a>

### FinishReason


```csharp
public string? FinishReason { get; init; }
```


The AI SDK finish reason that matters to the agent: [ContentFilter](#E2E_ModelResponse_ContentFilter) when the provider stopped the turn because it refused the request, or [ErrorFinish](#E2E_ModelResponse_ErrorFinish) when it ended on an error. Null for any other turn.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelResponse_RawFinishReason_"></a>
<a id="E2E_ModelResponse_RawFinishReason"></a>

### RawFinishReason


```csharp
public string? RawFinishReason { get; init; }
```


The provider's own word for the stop, such as `refusal`, when it sent one. Provider text: redact it before it reaches a message.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelResponse_ReasoningItems_"></a>
<a id="E2E_ModelResponse_ReasoningItems"></a>

### ReasoningItems


```csharp
public IReadOnlyList<JsonElement> ReasoningItems { get; init; }
```


The reasoning of the turn that the model can take back, to replay on the next turn. Empty when the model gave none.

**Value:** [IReadOnlyList&lt;JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_ModelResponse_ReasoningText_"></a>
<a id="E2E_ModelResponse_ReasoningText"></a>

### ReasoningText


```csharp
public string? ReasoningText { get; init; }
```


The reasoning text of the turn (upstream's `StepResult.reasoningText`), null when the model gave none. Provider text: the
step's `model` event carries it only after the redactor and a 600-character bound.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelResponse_ToolCalls_"></a>
<a id="E2E_ModelResponse_ToolCalls"></a>

### ToolCalls


```csharp
public IReadOnlyList<ModelToolCall> ToolCalls { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelToolCall&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_ModelResponse_Usage_"></a>
<a id="E2E_ModelResponse_Usage"></a>

### Usage


```csharp
public ModelCallUsage? Usage { get; init; }
```


**Value:** [ModelCallUsage](/api/E2E/ModelCallUsage/)

<a id="E2E_ModelResponse_Warnings_"></a>
<a id="E2E_ModelResponse_Warnings"></a>

### Warnings


```csharp
public IReadOnlyList<ModelWarning> Warnings { get; init; }
```


Upstream's call `warnings` (`StepResult.warnings`): what the provider changed in the request without failing it. Empty when it changed nothing.

**Value:** [IReadOnlyList&lt;ModelWarning&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
