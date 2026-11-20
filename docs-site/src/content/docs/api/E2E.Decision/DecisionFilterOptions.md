---
title: "DecisionFilterOptions"
description: "Options for a test filter that asks a decision model which tests a change can affect (upstream's DecisionFilterOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionFilterOptions"
  hidden: true
---

<a id="E2E_Decision_DecisionFilterOptions"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class DecisionFilterOptions
```


Options for a test filter that asks a decision model which tests a change can affect (upstream's `DecisionFilterOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Types.cs)

## Properties

<a id="E2E_Decision_DecisionFilterOptions_Model_"></a>
<a id="E2E_Decision_DecisionFilterOptions_Model"></a>

### Model


```csharp
public IDecisionModel? Model { get; init; }
```


A decision model that answers `boolean` questions with a probability. Required unless [ModelId](#E2E_Decision_DecisionFilterOptions_ModelId) is set, which is refused.

**Value:** [IDecisionModel](/api/E2E.Decision.Sdk/IDecisionModel/)

<a id="E2E_Decision_DecisionFilterOptions_ModelId_"></a>
<a id="E2E_Decision_DecisionFilterOptions_ModelId"></a>

### ModelId


```csharp
public string? ModelId { get; init; }
```


Stands for upstream's `model: 'typesafe-ai/jev'` model-id string, which this port types as [Model](#E2E_Decision_DecisionFilterOptions_Model). A model id
is not a decision model, so setting it without [Model](#E2E_Decision_DecisionFilterOptions_Model) is refused at config load as `INVALID_CONFIG`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_DecisionFilterOptions_ProviderOptions_"></a>
<a id="E2E_Decision_DecisionFilterOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Provider options sent with every decide call.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
