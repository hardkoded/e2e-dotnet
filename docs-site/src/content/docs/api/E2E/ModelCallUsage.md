---
title: "ModelCallUsage"
description: "ModelCallUsage in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelCallUsage"
  hidden: true
---

<a id="E2E_ModelCallUsage"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ModelCallUsage
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L97)

## Properties

<a id="E2E_ModelCallUsage_CacheReadTokens_"></a>
<a id="E2E_ModelCallUsage_CacheReadTokens"></a>

### CacheReadTokens


```csharp
public int? CacheReadTokens { get; init; }
```


Input tokens the provider served from its prompt cache, when it reports the split. A part of [InputTokens](#E2E_ModelCallUsage_InputTokens).

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L104)

<a id="E2E_ModelCallUsage_CacheWriteTokens_"></a>
<a id="E2E_ModelCallUsage_CacheWriteTokens"></a>

### CacheWriteTokens


```csharp
public int? CacheWriteTokens { get; init; }
```


Input tokens the provider wrote to its prompt cache, when it reports the split. A part of [InputTokens](#E2E_ModelCallUsage_InputTokens).

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L107)

<a id="E2E_ModelCallUsage_EstimatedCostUsd_"></a>
<a id="E2E_ModelCallUsage_EstimatedCostUsd"></a>

### EstimatedCostUsd


```csharp
public double? EstimatedCostUsd { get; init; }
```


The billed cost of the call in US dollars, when the provider reports one.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L110)

<a id="E2E_ModelCallUsage_InputTokens_"></a>
<a id="E2E_ModelCallUsage_InputTokens"></a>

### InputTokens


```csharp
public int InputTokens { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L99)

<a id="E2E_ModelCallUsage_OutputTokens_"></a>
<a id="E2E_ModelCallUsage_OutputTokens"></a>

### OutputTokens


```csharp
public int OutputTokens { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L101)
