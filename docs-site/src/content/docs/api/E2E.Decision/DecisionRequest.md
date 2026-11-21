---
title: "DecisionRequest"
description: "One decide call's input (upstream's DecisionRequest in questions.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionRequest"
  hidden: true
---

<a id="E2E_Decision_DecisionRequest"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class DecisionRequest
```


One decide call's input (upstream's `DecisionRequest` in `questions.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Questions.cs)

## Properties

<a id="E2E_Decision_DecisionRequest_Questions_"></a>
<a id="E2E_Decision_DecisionRequest_Questions"></a>

### Questions


```csharp
public required IReadOnlyDictionary<string, DecisionQuestion> Questions { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, DecisionQuestion&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_DecisionRequest_Screenshot_"></a>
<a id="E2E_Decision_DecisionRequest_Screenshot"></a>

### Screenshot


```csharp
public Screenshot? Screenshot { get; init; }
```


Masked viewport pixels the model sees beside the state, when vision is on and pixels were granted. A text-only provider rejects it before the request.

**Value:** [Screenshot](/api/E2E.Decision/Screenshot/)

<a id="E2E_Decision_DecisionRequest_State_"></a>
<a id="E2E_Decision_DecisionRequest_State"></a>

### State


```csharp
public required JsonElement State { get; init; }
```


The JSON state every question reads.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)
