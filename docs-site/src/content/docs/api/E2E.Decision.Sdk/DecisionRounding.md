---
title: "DecisionRounding"
description: "Declared rounding precision of the numbers a model returns (decimal places, 0 through 15)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionRounding"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionRounding"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionRounding : IEquatable<DecisionRounding>
```


Declared rounding precision of the numbers a model returns (decimal places, 0 through 15).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionRounding&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionRounding__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionRounding__ctor_System_Nullable_System_Int32__System_Nullable_System_Int32__"></a>

### DecisionRounding(int?, int?)


```csharp
public DecisionRounding(int? ProbabilityDecimals = null, int? ScoreDecimals = null)
```


Declared rounding precision of the numbers a model returns (decimal places, 0 through 15).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ProbabilityDecimals` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `ScoreDecimals` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionRounding_ProbabilityDecimals_"></a>
<a id="E2E_Decision_Sdk_DecisionRounding_ProbabilityDecimals"></a>

### ProbabilityDecimals


```csharp
public int? ProbabilityDecimals { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecisionRounding_ScoreDecimals_"></a>
<a id="E2E_Decision_Sdk_DecisionRounding_ScoreDecimals"></a>

### ScoreDecimals


```csharp
public int? ScoreDecimals { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
