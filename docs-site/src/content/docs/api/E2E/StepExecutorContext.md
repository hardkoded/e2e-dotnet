---
title: "StepExecutorContext"
description: "What an executor receives for one step (upstream's StepExecutorContext). The runtime builds it. A test of an executor can build its own and override the memb..."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepExecutorContext"
  hidden: true
---

<a id="E2E_StepExecutorContext"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class StepExecutorContext
```


What an executor receives for one step (upstream's `StepExecutorContext`). The runtime builds it. A test of an
executor can build its own and override the members it reads: each one that does work throws until it is overridden.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L69)

## Properties

<a id="E2E_StepExecutorContext_Actions_"></a>
<a id="E2E_StepExecutorContext_Actions"></a>

### Actions


```csharp
public required IExecutorActions Actions { get; init; }
```


**Value:** [IExecutorActions](/api/E2E/IExecutorActions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L99)

<a id="E2E_StepExecutorContext_AgentContext_"></a>
<a id="E2E_StepExecutorContext_AgentContext"></a>

### AgentContext


```csharp
public string? AgentContext { get; init; }
```


Trusted project/test context, redacted: the agent's `Context`, then the test's `agentContext`, one per line. Null when
both are blank.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L95)

<a id="E2E_StepExecutorContext_Attempt_"></a>
<a id="E2E_StepExecutorContext_Attempt"></a>

### Attempt


```csharp
public required ExecutorAttempt Attempt { get; init; }
```


The attempt this step runs in: its identity, the signal that ends it, and the scratch memory.

**Value:** [ExecutorAttempt](/api/E2E/ExecutorAttempt/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L74)

<a id="E2E_StepExecutorContext_Budgets_"></a>
<a id="E2E_StepExecutorContext_Budgets"></a>

### Budgets


```csharp
public required IExecutorBudgets Budgets { get; init; }
```


**Value:** [IExecutorBudgets](/api/E2E/IExecutorBudgets/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L97)

<a id="E2E_StepExecutorContext_Ledger_"></a>
<a id="E2E_StepExecutorContext_Ledger"></a>

### Ledger


```csharp
public string Ledger { get; init; }
```


The completed earlier steps of this attempt, serialized for prompt context, one per line, oldest first. Empty when none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L89)

<a id="E2E_StepExecutorContext_Model_"></a>
<a id="E2E_StepExecutorContext_Model"></a>

### Model


```csharp
public IAgentModel? Model { get; init; }
```


The model the agent's entry is configured with, or the one the executor brought. Null when there is none.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L83)

<a id="E2E_StepExecutorContext_ProviderOptions_"></a>
<a id="E2E_StepExecutorContext_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The agent's provider options, keyed by provider. The built-in executor sends them with every model call unless it was given its own.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L86)

<a id="E2E_StepExecutorContext_ReplayedPrefix_"></a>
<a id="E2E_StepExecutorContext_ReplayedPrefix"></a>

### ReplayedPrefix


```csharp
public ReplayedPrefix? ReplayedPrefix { get; init; }
```


Present when a cached replay ran part of this step before it handed the step over.

**Value:** [ReplayedPrefix](/api/E2E/ReplayedPrefix/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L80)

<a id="E2E_StepExecutorContext_Step_"></a>
<a id="E2E_StepExecutorContext_Step"></a>

### Step


```csharp
public required ExecutorStep Step { get; init; }
```


**Value:** [ExecutorStep](/api/E2E/ExecutorStep/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L71)

<a id="E2E_StepExecutorContext_Target_"></a>
<a id="E2E_StepExecutorContext_Target"></a>

### Target


```csharp
public required ExecutorTargetInfo Target { get; init; }
```


The target this step runs on.

**Value:** [ExecutorTargetInfo](/api/E2E/ExecutorTargetInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L77)

## Methods

<a id="E2E_StepExecutorContext_AttachScreenshotAsync_"></a>
<a id="E2E_StepExecutorContext_AttachScreenshotAsync_E2E_ExecutorPixels_System_String_System_Threading_CancellationToken_"></a>

### AttachScreenshotAsync(ExecutorPixels, string, CancellationToken)


```csharp
public virtual Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default)
```


Keeps pixels the executor or one of its tools observed as a screenshot of the step in progress, and returns the
path of the file, relative to the project root. The file is named by &lt;code class="paramref"&gt;label&lt;/code&gt; and lands in the
attempt's results directory. For a tool with evidence worth keeping.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pixels` | [ExecutorPixels](/api/E2E/ExecutorPixels/) |  |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L143)

<a id="E2E_StepExecutorContext_AttachTranscript_"></a>
<a id="E2E_StepExecutorContext_AttachTranscript_System_String_"></a>

### AttachTranscript(string)


```csharp
public virtual void AttachTranscript(string text)
```


Attaches the executor's model transcript to the step. It is written to the results directory as a `log` file
when the attempt keeps results, and dropped otherwise. Call it once, at the end.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L125)

<a id="E2E_StepExecutorContext_AttachTurns_"></a>
<a id="E2E_StepExecutorContext_AttachTurns_System_Collections_Generic_IReadOnlyList_E2E_StepTurn__"></a>

### `AttachTurns(IReadOnlyList<StepTurn>)`


```csharp
public virtual void AttachTurns(IReadOnlyList<StepTurn> turns)
```


Keeps a bounded record of the step's model turns on the step record: each turn's tool calls and what came back,
clipped. The report shows the last turns under a failed step, so a reader sees what the model did and saw without the
transcript. Call it as each turn ends; the last call wins.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `turns` | [IReadOnlyList&lt;StepTurn&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L134)

<a id="E2E_StepExecutorContext_ObserveAsync_"></a>
<a id="E2E_StepExecutorContext_ObserveAsync_E2E_ExecutorObserveOptions_System_Threading_CancellationToken_"></a>

### ObserveAsync(ExecutorObserveOptions?, CancellationToken)


```csharp
public virtual Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default)
```


Captures one fresh, redacted observation. The text is always present; the tree and the pixels are opt-in, so an
executor that renders its own view of the screen asks for exactly what it uses.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ExecutorObserveOptions](/api/E2E/ExecutorObserveOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ExecutorObservation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L118)
