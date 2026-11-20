---
title: "Recorded<T>"
description: "What one model call came back with: its value, the tokens it used, and the model id the response named (upstream's Recorded)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Recorded<T>"
  hidden: true
---

<a id="E2E_Decision_Recorded_1"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class Recorded<T>
```


What one model call came back with: its value, the tokens it used, and the model id the response named (upstream's `Recorded`).

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Budget.cs)

## Properties

<a id="E2E_Decision_Recorded_1_InputTokens_"></a>
<a id="E2E_Decision_Recorded_1_InputTokens"></a>

### InputTokens


```csharp
public int? InputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Recorded_1_ModelId_"></a>
<a id="E2E_Decision_Recorded_1_ModelId"></a>

### ModelId


```csharp
public string? ModelId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Recorded_1_OutputTokens_"></a>
<a id="E2E_Decision_Recorded_1_OutputTokens"></a>

### OutputTokens


```csharp
public int? OutputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Recorded_1_Value_"></a>
<a id="E2E_Decision_Recorded_1_Value"></a>

### Value


```csharp
public required T Value { get; init; }
```


**Value:** `T`
