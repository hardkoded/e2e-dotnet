---
title: "TypeSafeAiDecisionModelOptions"
description: "The AI SDK's typesafeAiDecisionModelOptions: the typesafe provider options of a decision call."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeSafeAiDecisionModelOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelOptions"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed class TypeSafeAiDecisionModelOptions
```


The AI SDK's `typesafeAiDecisionModelOptions`: the `typesafe` provider options of a decision call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiDecisionModelOptions.cs)

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelOptions_LongContext_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiDecisionModelOptions_LongContext"></a>

### LongContext


```csharp
public bool? LongContext { get; init; }
```


Enables TypeSafe's experimental long context mode, raising both the per-request and per-question context limits to 262k tokens. Long
context is slower and less accurate than the default limits. Defaults to false.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)
