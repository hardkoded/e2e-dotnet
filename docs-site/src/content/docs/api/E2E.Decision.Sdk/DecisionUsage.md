---
title: "DecisionUsage"
description: "Tokens a decision call used."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionUsage"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionUsage"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionUsage : IEquatable<DecisionUsage>
```


Tokens a decision call used.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionUsage&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionUsage__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionUsage__ctor_System_Nullable_System_Int32__System_Nullable_System_Int32__"></a>

### DecisionUsage(int?, int?)


```csharp
public DecisionUsage(int? InputTokens, int? OutputTokens)
```


Tokens a decision call used.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `InputTokens` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `OutputTokens` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionUsage_InputTokens_"></a>
<a id="E2E_Decision_Sdk_DecisionUsage_InputTokens"></a>

### InputTokens


```csharp
public int? InputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecisionUsage_OutputTokens_"></a>
<a id="E2E_Decision_Sdk_DecisionUsage_OutputTokens"></a>

### OutputTokens


```csharp
public int? OutputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecisionUsage_TotalTokens_"></a>
<a id="E2E_Decision_Sdk_DecisionUsage_TotalTokens"></a>

### TotalTokens


```csharp
public int? TotalTokens { get; }
```


The sum of both counts, or null when either is unknown.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
