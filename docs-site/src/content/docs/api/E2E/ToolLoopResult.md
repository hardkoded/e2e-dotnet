---
title: "ToolLoopResult"
description: "What a tool call answered: the text the model reads, whether the call failed, which the loop counts toward its failure streak, and the screenshot that goes w..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ToolLoopResult"
  hidden: true
---

<a id="E2E_ToolLoopResult"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public readonly record struct ToolLoopResult : IEquatable<ToolLoopResult>
```


What a tool call answered: the text the model reads, whether the call failed, which the loop counts toward its failure streak, and the
screenshot that goes with the text, when there is one (upstream's tool result with a file part). The loop hands the image to the model
in a user turn right after the turn's tool results, since a tool message carries text only on every provider the port sends to.

**Implements:** [IEquatable&lt;ToolLoopResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs)

## Constructors

<a id="E2E_ToolLoopResult__ctor_"></a>
<a id="E2E_ToolLoopResult__ctor_System_String_System_Boolean_E2E_ExecutorPixels_System_Boolean_"></a>

### ToolLoopResult(string, bool, ExecutorPixels?, bool)


```csharp
public ToolLoopResult(string Content, bool Failed = false, ExecutorPixels? Pixels = null, bool ErrorText = false)
```


What a tool call answered: the text the model reads, whether the call failed, which the loop counts toward its failure streak, and the
screenshot that goes with the text, when there is one (upstream's tool result with a file part). The loop hands the image to the model
in a user turn right after the turn's tool results, since a tool message carries text only on every provider the port sends to.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Content` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Failed` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Pixels` | [ExecutorPixels](/api/E2E/ExecutorPixels/) |  |
| `ErrorText` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

## Properties

<a id="E2E_ToolLoopResult_Content_"></a>
<a id="E2E_ToolLoopResult_Content"></a>

### Content


```csharp
public string Content { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ToolLoopResult_ErrorText_"></a>
<a id="E2E_ToolLoopResult_ErrorText"></a>

### ErrorText


```csharp
public bool ErrorText { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_ToolLoopResult_Failed_"></a>
<a id="E2E_ToolLoopResult_Failed"></a>

### Failed


```csharp
public bool Failed { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_ToolLoopResult_Pixels_"></a>
<a id="E2E_ToolLoopResult_Pixels"></a>

### Pixels


```csharp
public ExecutorPixels? Pixels { get; init; }
```


**Value:** [ExecutorPixels](/api/E2E/ExecutorPixels/)
