---
title: "DecisionStatePart"
description: "A part of the state every question of a decision call shares (the AI SDK's DecisionModelV4State)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionStatePart"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionStatePart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public abstract record DecisionStatePart : IEquatable<DecisionStatePart>
```


A part of the state every question of a decision call shares (the AI SDK's `DecisionModelV4State`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [DecisionFilePart](/api/E2E.Decision.Sdk/DecisionFilePart/), [DecisionFileUrlPart](/api/E2E.Decision.Sdk/DecisionFileUrlPart/), [DecisionJsonPart](/api/E2E.Decision.Sdk/DecisionJsonPart/), [DecisionTextPart](/api/E2E.Decision.Sdk/DecisionTextPart/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)
