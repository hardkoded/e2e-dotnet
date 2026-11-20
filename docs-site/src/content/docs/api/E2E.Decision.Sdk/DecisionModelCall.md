---
title: "DecisionModelCall"
description: "What DecisionModelCall hands a decision model (the AI SDK's DecisionModelV4CallOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionModelCall"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionModelCall"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionModelCall
```


What [DecisionModelCall](./) hands a decision model (the AI SDK's `DecisionModelV4CallOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionModelCall_Headers_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCall_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string> Headers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelCall_MaxOutputTokens_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCall_MaxOutputTokens"></a>

### MaxOutputTokens


```csharp
public int? MaxOutputTokens { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DecisionModelCall_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCall_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The provider options, keyed by provider name. Each value is a JSON object.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelCall_Questions_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCall_Questions"></a>

### Questions


```csharp
public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, DecisionQuestion&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionModelCall_Reasoning_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCall_Reasoning"></a>

### Reasoning


```csharp
public string? Reasoning { get; init; }
```


Reasoning effort: `provider-default`, `none`, `minimal`, `low`, `medium`, `high`, `xhigh`, or `max`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionModelCall_State_"></a>
<a id="E2E_Decision_Sdk_DecisionModelCall_State"></a>

### State


```csharp
public required IReadOnlyList<DecisionStatePart> State { get; init; }
```


**Value:** [IReadOnlyList&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
