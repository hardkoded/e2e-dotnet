---
title: "ExperimentalDecide"
description: "The AI SDK's experimental_decide (ai/src/decide/decide.ts) and its input and answer validation (validate-decision.ts), which the SDK keeps in its ai package..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExperimentalDecide"
  hidden: true
---

<a id="E2E_Decision_Sdk_ExperimentalDecide"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class ExperimentalDecide
```


The AI SDK's `experimental_decide` (`ai/src/decide/decide.ts`) and its input and answer validation
(`validate-decision.ts`), which the SDK keeps in its `ai` package and the port has no such package for. .NET-only home.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ExperimentalDecide.cs)

## Fields

<a id="E2E_Decision_Sdk_ExperimentalDecide_ExperimentalEvaluate"></a>

### ExperimentalEvaluate


```csharp
public static readonly Func<DecideOptions, CancellationToken, Task<DecisionResult>> ExperimentalEvaluate
```


Deprecated: use `RunAsync`. The AI SDK's `experimental_evaluate`, the same function as `experimental_decide`.

**Value:** [Func&lt;DecideOptions, CancellationToken, Task&lt;DecisionResult&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

## Methods

<a id="E2E_Decision_Sdk_ExperimentalDecide_RunAsync_"></a>
<a id="E2E_Decision_Sdk_ExperimentalDecide_RunAsync_E2E_Decision_Sdk_DecideOptions_System_Threading_CancellationToken_"></a>

### RunAsync(DecideOptions, CancellationToken)


```csharp
public static Task<DecisionResult> RunAsync(DecideOptions options, CancellationToken cancellationToken = default)
```


Decides answers to typed questions against one shared state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [DecideOptions](/api/E2E.Decision.Sdk/DecideOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DecisionResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
