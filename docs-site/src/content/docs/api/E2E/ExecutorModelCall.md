---
title: "ExecutorModelCall"
description: "Usage detail of one model call an executor made. Every field is optional."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorModelCall"
  hidden: true
---

<a id="E2E_ExecutorModelCall"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorModelCall
```


Usage detail of one model call an executor made. Every field is optional.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L461)

## Properties

<a id="E2E_ExecutorModelCall_CacheReadTokens_"></a>
<a id="E2E_ExecutorModelCall_CacheReadTokens"></a>

### CacheReadTokens


```csharp
public int? CacheReadTokens { get; init; }
```


Input tokens the provider served from its prompt cache, when it reports the split.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L468)

<a id="E2E_ExecutorModelCall_CacheWriteTokens_"></a>
<a id="E2E_ExecutorModelCall_CacheWriteTokens"></a>

### CacheWriteTokens


```csharp
public int? CacheWriteTokens { get; init; }
```


Input tokens written to the provider's prompt cache, when it reports the split.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L471)

<a id="E2E_ExecutorModelCall_DurationMs_"></a>
<a id="E2E_ExecutorModelCall_DurationMs"></a>

### DurationMs


```csharp
public int? DurationMs { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L475)

<a id="E2E_ExecutorModelCall_EstimatedCostUsd_"></a>
<a id="E2E_ExecutorModelCall_EstimatedCostUsd"></a>

### EstimatedCostUsd


```csharp
public double? EstimatedCostUsd { get; init; }
```


The billed cost of the call in US dollars, when the provider reports one.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L482)

<a id="E2E_ExecutorModelCall_InputTokens_"></a>
<a id="E2E_ExecutorModelCall_InputTokens"></a>

### InputTokens


```csharp
public int? InputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L463)

<a id="E2E_ExecutorModelCall_ModelId_"></a>
<a id="E2E_ExecutorModelCall_ModelId"></a>

### ModelId


```csharp
public string? ModelId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L479)

<a id="E2E_ExecutorModelCall_OutputTokens_"></a>
<a id="E2E_ExecutorModelCall_OutputTokens"></a>

### OutputTokens


```csharp
public int? OutputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L465)

<a id="E2E_ExecutorModelCall_Provider_"></a>
<a id="E2E_ExecutorModelCall_Provider"></a>

### Provider


```csharp
public string? Provider { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L477)

<a id="E2E_ExecutorModelCall_Reasoning_"></a>
<a id="E2E_ExecutorModelCall_Reasoning"></a>

### Reasoning


```csharp
public string? Reasoning { get; init; }
```


The model's own reasoning for the turn, when it produced one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L485)

<a id="E2E_ExecutorModelCall_StartedAt_"></a>
<a id="E2E_ExecutorModelCall_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset? StartedAt { get; init; }
```


**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L473)
