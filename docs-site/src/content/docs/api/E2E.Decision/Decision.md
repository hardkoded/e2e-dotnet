---
title: "Decision"
description: "One gated answer: the chosen option, its probability, and the provider confidence. A score answer reads its most probable level as the choice, carries the pr..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Decision"
  hidden: true
---

<a id="E2E_Decision_Decision"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Decision : IEquatable<Decision>
```


One gated answer: the chosen option, its probability, and the provider confidence. A score answer reads its most probable level as the
choice, carries the probability-weighted level as [Score](#E2E_Decision_Decision_Score), and its probability is the mass on the levels within half a level
of that score: a spread over two adjacent columns is a precise position, not an uncertain one.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Decision&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Decide.cs)

## Constructors

<a id="E2E_Decision_Decision__ctor_"></a>
<a id="E2E_Decision_Decision__ctor_System_String_System_Double_System_Double_System_Nullable_System_Double__"></a>

### Decision(string, double, double, double?)


```csharp
public Decision(string Choice, double Probability, double Confidence, double? Score = null)
```


One gated answer: the chosen option, its probability, and the provider confidence. A score answer reads its most probable level as the
choice, carries the probability-weighted level as [Score](#E2E_Decision_Decision_Score), and its probability is the mass on the levels within half a level
of that score: a spread over two adjacent columns is a precise position, not an uncertain one.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Choice` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Probability` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Confidence` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Score` | [double?](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Decision_Decision_Choice_"></a>
<a id="E2E_Decision_Decision_Choice"></a>

### Choice


```csharp
public string Choice { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Decision_Confidence_"></a>
<a id="E2E_Decision_Decision_Confidence"></a>

### Confidence


```csharp
public double Confidence { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Decision_Probability_"></a>
<a id="E2E_Decision_Decision_Probability"></a>

### Probability


```csharp
public double Probability { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Decision_Score_"></a>
<a id="E2E_Decision_Decision_Score"></a>

### Score


```csharp
public double? Score { get; init; }
```


**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)
