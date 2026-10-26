---
title: "ToolLoopResult"
description: "What a tool call answered: the text the model reads, and whether the call failed, which the loop counts toward its failure streak."
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


What a tool call answered: the text the model reads, and whether the call failed, which the loop counts toward its failure streak.

**Implements:** [IEquatable&lt;ToolLoopResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L89)

## Constructors

<a id="E2E_ToolLoopResult__ctor_"></a>
<a id="E2E_ToolLoopResult__ctor_System_String_System_Boolean_"></a>

### ToolLoopResult(string, bool)


```csharp
public ToolLoopResult(string Content, bool Failed = false)
```


What a tool call answered: the text the model reads, and whether the call failed, which the loop counts toward its failure streak.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Content` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Failed` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L89)

## Properties

<a id="E2E_ToolLoopResult_Content_"></a>
<a id="E2E_ToolLoopResult_Content"></a>

### Content


```csharp
public string Content { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L89)

<a id="E2E_ToolLoopResult_Failed_"></a>
<a id="E2E_ToolLoopResult_Failed"></a>

### Failed


```csharp
public bool Failed { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L89)
