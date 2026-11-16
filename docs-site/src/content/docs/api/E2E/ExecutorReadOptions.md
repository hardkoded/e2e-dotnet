---
title: "ExecutorReadOptions"
description: "What an executor asks ReadAsync to measure on a node (upstream's ExecutorReadOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorReadOptions"
  hidden: true
---

<a id="E2E_ExecutorReadOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorReadOptions
```


What an executor asks `ReadAsync` to measure on a node (upstream's `ExecutorReadOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Properties

<a id="E2E_ExecutorReadOptions_Style_"></a>
<a id="E2E_ExecutorReadOptions_Style"></a>

### Style


```csharp
public required IReadOnlyList<string> Style { get; init; }
```


A non-empty list of allow-listed CSS property names, in kebab case ([ComputedStyleProperties](/api/E2E.Engine/ComputedStyles/#E2E_Engine_ComputedStyles_ComputedStyleProperties)).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
