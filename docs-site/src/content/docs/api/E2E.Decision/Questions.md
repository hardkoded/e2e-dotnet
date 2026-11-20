---
title: "Questions"
description: "Upstream's questions.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Questions"
  hidden: true
---

<a id="E2E_Decision_Questions"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Questions
```


Upstream's `questions.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Questions.cs)

## Fields

<a id="E2E_Decision_Questions_None"></a>

### None


```csharp
public const string None = "none"
```


The fallback every target question carries (upstream's `NONE`). A provider that finds no applicable target for an operation the
step will not take (OpenAI refuses such a question outright) chooses this instead, and the step reads it as "not this operation".

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Questions_AssertionRequest_"></a>
<a id="E2E_Decision_Questions_AssertionRequest_System_String_System_String_E2E_Decision_ActionSpace_E2E_Decision_Screenshot_"></a>

### AssertionRequest(string, string, ActionSpace?, Screenshot?)


```csharp
public static DecisionRequest AssertionRequest(string goal, string path, ActionSpace? space, Screenshot? screenshot)
```


Upstream's `assertionRequest`. Builds an assertion verdict request: the goal, the page, and the element table. The screen alone, no history.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `goal` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `space` | [ActionSpace](/api/E2E.Decision/ActionSpace/) |  |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |

**Returns:** [DecisionRequest](/api/E2E.Decision/DecisionRequest/)

<a id="E2E_Decision_Questions_CompletionRequest_"></a>
<a id="E2E_Decision_Questions_CompletionRequest_E2E_StepExecutorContext_E2E_Decision_ActionSpace_System_Collections_Generic_IReadOnlyList_E2E_Decision_HistoryEntry__System_String_E2E_Decision_Screenshot_System_String_"></a>

### `CompletionRequest(StepExecutorContext, ActionSpace, IReadOnlyList<HistoryEntry>, string, Screenshot?, string?)`


```csharp
public static DecisionRequest CompletionRequest(StepExecutorContext ctx, ActionSpace space, IReadOnlyList<HistoryEntry> history, string path, Screenshot? screenshot, string? origin = null)
```


Upstream's `completionRequest`. Builds a completion-check request: the goal with its non-secret params, the page, and the element
table in the state; the actions the step took, with the values it typed, in the question only. The same list in the state was measured
on gpt-6-luna to pull a verdict toward inconclusive (holds 0.17 with it, 0.51 without, on a checked checkbox). Rejected done/failed
claims are left out (they are not actions), and so is any model reasoning.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `space` | [ActionSpace](/api/E2E.Decision/ActionSpace/) |  |
| `history` | [IReadOnlyList&lt;HistoryEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `origin` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionRequest](/api/E2E.Decision/DecisionRequest/)

<a id="E2E_Decision_Questions_OperationRequest_"></a>
<a id="E2E_Decision_Questions_OperationRequest_E2E_StepExecutorContext_E2E_Decision_ActionSpace_System_Collections_Generic_IReadOnlyList_E2E_Decision_HistoryEntry__System_String_"></a>

### `OperationRequest(StepExecutorContext, ActionSpace, IReadOnlyList<HistoryEntry>, string)`


```csharp
public static DecisionRequest OperationRequest(StepExecutorContext ctx, ActionSpace space, IReadOnlyList<HistoryEntry> history, string path)
```


Upstream's `operationRequest`. Builds the operation request: one choice over the operations the screen offers, the controls,
and the terminals. Every question states the goal and the recent actions in its own instructions: a classifier reads the question
before the evidence.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `space` | [ActionSpace](/api/E2E.Decision/ActionSpace/) |  |
| `history` | [IReadOnlyList&lt;HistoryEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionRequest](/api/E2E.Decision/DecisionRequest/)

<a id="E2E_Decision_Questions_PointRequest_"></a>
<a id="E2E_Decision_Questions_PointRequest_E2E_StepExecutorContext_System_String_E2E_Decision_Screenshot_E2E_Decision_Grid_System_String_System_String_"></a>

### PointRequest(StepExecutorContext, string, Screenshot, Grid, string, string)


```csharp
public static DecisionRequest PointRequest(StepExecutorContext ctx, string path, Screenshot image, Grid grid, string framing, string target)
```


Upstream's `pointRequest`. Builds a point request: two score questions over the grid on the image, one for the column and one
for the row of the control the goal needs (&lt;code class="paramref"&gt;target&lt;/code&gt;, as the text model named it). The probability-weighted means
give a point finer than any one cell. The state is the goal alone: the element table adds nothing to where a drawn control sits,
and measured on gpt-6-luna it pulls the estimate off. A dependent decision goes in its own request, as the Decisions API asks.
Sizes are in the pixels of the image the model sees: the runner hands over a downscaled capture, and a model told CSS sizes for a
smaller image lands its estimate short.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `image` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `grid` | [Grid](/api/E2E.Decision/Grid/) |  |
| `framing` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionRequest](/api/E2E.Decision/DecisionRequest/)

<a id="E2E_Decision_Questions_TargetRequest_"></a>
<a id="E2E_Decision_Questions_TargetRequest_E2E_StepExecutorContext_E2E_Decision_ActionSpace_E2E_Decision_Operation_System_Collections_Generic_IReadOnlyList_E2E_Decision_HistoryEntry__System_String_"></a>

### `TargetRequest(StepExecutorContext, ActionSpace, Operation, IReadOnlyList<HistoryEntry>, string)`


```csharp
public static DecisionRequest TargetRequest(StepExecutorContext ctx, ActionSpace space, Operation operation, IReadOnlyList<HistoryEntry> history, string path)
```


Upstream's `targetRequest`. Builds the target request that follows a chosen operation: its target question, the destination
question for a drag, and the secret question for a secret fill with two or more secrets declared. A lone target is asked too: its
`none` is how a wrong operation stops, where dispatching it would act on the one element the operation happened to fit. Every
target question carries the `none` fallback, so a provider never has to refuse one. A dependent decision goes in its own
request, as the Decisions API asks.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `space` | [ActionSpace](/api/E2E.Decision/ActionSpace/) |  |
| `operation` | [Operation](/api/E2E.Decision/Operation/) |  |
| `history` | [IReadOnlyList&lt;HistoryEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [DecisionRequest](/api/E2E.Decision/DecisionRequest/)
