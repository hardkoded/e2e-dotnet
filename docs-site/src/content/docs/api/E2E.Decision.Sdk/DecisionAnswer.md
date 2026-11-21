---
title: "DecisionAnswer"
description: "One model answer (the AI SDK's DecisionAnswer). Numbers are returned as the provider sent them. Type is the question's type, or refusal when the model declin..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionAnswer"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionAnswer"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionAnswer
```


One model answer (the AI SDK's `DecisionAnswer`). Numbers are returned as the provider sent them.
[Type](#E2E_Decision_Sdk_DecisionAnswer_Type) is the question's type, or `refusal` when the model declined the question.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionAnswer_Box_"></a>
<a id="E2E_Decision_Sdk_DecisionAnswer_Box"></a>

### Box


```csharp
public DecisionBox? Box { get; init; }
```


The normalized box of a `box` answer.

**Value:** [DecisionBox](/api/E2E.Decision.Sdk/DecisionBox/)

<a id="E2E_Decision_Sdk_DecisionAnswer_Choice_"></a>
<a id="E2E_Decision_Sdk_DecisionAnswer_Choice"></a>

### Choice


```csharp
public string? Choice { get; init; }
```


The selected option of a `choice` answer.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionAnswer_Probabilities_"></a>
<a id="E2E_Decision_Sdk_DecisionAnswer_Probabilities"></a>

### Probabilities


```csharp
public IReadOnlyDictionary<string, double>? Probabilities { get; init; }
```


The distribution of a `choice` or `score` answer: option ids, or level indexes, to probabilities. Null when the model gave none.

**Value:** [IReadOnlyDictionary&lt;string, double&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionAnswer_Probability_"></a>
<a id="E2E_Decision_Sdk_DecisionAnswer_Probability"></a>

### Probability


```csharp
public double? Probability { get; init; }
```


P(true) of a `boolean` answer.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_DecisionAnswer_Score_"></a>
<a id="E2E_Decision_Sdk_DecisionAnswer_Score"></a>

### Score


```csharp
public double? Score { get; init; }
```


The score of a `score` answer.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_DecisionAnswer_Type_"></a>
<a id="E2E_Decision_Sdk_DecisionAnswer_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
