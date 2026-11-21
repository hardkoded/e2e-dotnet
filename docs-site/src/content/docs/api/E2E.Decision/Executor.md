---
title: "Executor"
description: "Upstream's executor.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Executor"
  hidden: true
---

<a id="E2E_Decision_Executor"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Executor
```


Upstream's `executor.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Decision_Executor_DecisionExecutor_"></a>
<a id="E2E_Decision_Executor_DecisionExecutor_E2E_Decision_DecisionExecutorOptions_"></a>

### DecisionExecutor(DecisionExecutorOptions)


```csharp
public static IStepExecutor DecisionExecutor(DecisionExecutorOptions options)
```


Upstream's `decisionExecutor`. Builds a step executor that acts through a decision model and an optional text model.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [DecisionExecutorOptions](/api/E2E.Decision/DecisionExecutorOptions/) |  |

**Returns:** [IStepExecutor](/api/E2E/IStepExecutor/)
