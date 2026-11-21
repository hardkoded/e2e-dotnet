---
title: "Dispatch"
description: "Upstream's dispatch.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Dispatch"
  hidden: true
---

<a id="E2E_Decision_Dispatch"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Dispatch
```


Upstream's `dispatch.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Dispatch.cs)

## Methods

<a id="E2E_Decision_Dispatch_PerformAsync_"></a>
<a id="E2E_Decision_Dispatch_PerformAsync_E2E_StepExecutorContext_E2E_Decision_DecisionAction_System_Threading_CancellationToken_"></a>

### PerformAsync(StepExecutorContext, DecisionAction, CancellationToken)


```csharp
public static Task<string?> PerformAsync(StepExecutorContext ctx, DecisionAction action, CancellationToken cancellationToken = default)
```


Upstream's `perform`. Performs one action through the runner's grammar: the only place that touches `ctx.actions`. Resolves
to a note for the model when the engine reports something worth telling it, nothing otherwise.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `action` | [DecisionAction](/api/E2E.Decision/DecisionAction/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
