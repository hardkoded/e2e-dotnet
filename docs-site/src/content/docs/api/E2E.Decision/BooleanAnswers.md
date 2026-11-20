---
title: "BooleanAnswers"
description: "One call's boolean answers and what it spent (upstream's BooleanAnswers)."
editUrl: false
prev: false
next: false
sidebar:
  label: "BooleanAnswers"
  hidden: true
---

<a id="E2E_Decision_BooleanAnswers"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class BooleanAnswers
```


One call's boolean answers and what it spent (upstream's `BooleanAnswers`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Decide.cs)

## Properties

<a id="E2E_Decision_BooleanAnswers_Probabilities_"></a>
<a id="E2E_Decision_BooleanAnswers_Probabilities"></a>

### Probabilities


```csharp
public required IReadOnlyDictionary<string, double?> Probabilities { get; init; }
```


Each answer's probability of true, by question id; null where the model gave none.

**Value:** [IReadOnlyDictionary&lt;string, double?&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_BooleanAnswers_ProviderMetadata_"></a>
<a id="E2E_Decision_BooleanAnswers_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public JsonElement? ProviderMetadata { get; init; }
```


**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_BooleanAnswers_Usage_"></a>
<a id="E2E_Decision_BooleanAnswers_Usage"></a>

### Usage


```csharp
public required DecisionUsage Usage { get; init; }
```


**Value:** [DecisionUsage](/api/E2E.Decision.Sdk/DecisionUsage/)
