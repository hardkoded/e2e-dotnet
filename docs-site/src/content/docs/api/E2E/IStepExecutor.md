---
title: "IStepExecutor"
description: "Upstream's StepExecutor (agent/executor.ts): the part of an act step that thinks. The runtime owns the step's cache, budget, deadline, and verdict; an execut..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IStepExecutor"
  hidden: true
---

<a id="E2E_IStepExecutor"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IStepExecutor
```


Upstream's `StepExecutor` (`agent/executor.ts`): the part of an act step that thinks. The runtime owns
the step's cache, budget, deadline, and verdict; an executor reads the screen, acts through the context, and
returns a verdict. Swapping executors changes the thinking, never the safety, the budgets, or the verdict grammar.

&lt;p&gt;
An executor is trusted project code, in the same trust domain as the test project that builds it: it may hold its
own model and credentials. What the runtime enforces against it is accounting, not secrecy: the budgets, the
deadline, the recording, and the verdict grammar hold whoever thinks.
&lt;/p&gt;

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Properties

<a id="E2E_IStepExecutor_Cache_"></a>
<a id="E2E_IStepExecutor_Cache"></a>

### Cache


```csharp
ExecutorCache Cache { get; }
```


How the executor takes part in the step cache. Upstream's `cache`.

**Value:** [ExecutorCache](/api/E2E/ExecutorCache/)

<a id="E2E_IStepExecutor_Judge_"></a>
<a id="E2E_IStepExecutor_Judge"></a>

### Judge


```csharp
IAgentModel? Judge { get; }
```


The model the judgments (`assert` through [AssertOptions](/api/E2E/AssertOptions/), `waitFor`, and `extract`) call
for this executor's agent, when it differs from [Model](#E2E_IStepExecutor_Model). `INVALID_CONFIG` when the entry sets another.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

<a id="E2E_IStepExecutor_Model_"></a>
<a id="E2E_IStepExecutor_Model"></a>

### Model


```csharp
IAgentModel? Model { get; }
```


The model this executor brought, if it has one. Config resolution reads it as the agent's model when the entry sets
none, so one executor drives both `act` and the judgments. `INVALID_CONFIG` when both are set and differ.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

<a id="E2E_IStepExecutor_Name_"></a>
<a id="E2E_IStepExecutor_Name"></a>

### Name


```csharp
string Name { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_IStepExecutor_Version_"></a>
<a id="E2E_IStepExecutor_Version"></a>

### Version


```csharp
string? Version { get; }
```


The version this executor reports. Upstream's `version`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_IStepExecutor_Vision_"></a>
<a id="E2E_IStepExecutor_Vision"></a>

### Vision


```csharp
bool Vision { get; }
```


Whether the executor honors [Vision](/api/E2E/AssertOptions/#E2E_AssertOptions_Vision): the mode reaches it as [Vision](/api/E2E/ExecutorStep/#E2E_ExecutorStep_Vision). Without
this, the option fails the call with `UNSUPPORTED_CAPABILITY`, since only the executor decides what its model sees. Upstream's `vision`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_IStepExecutor_RunStepAsync_"></a>
<a id="E2E_IStepExecutor_RunStepAsync_E2E_StepExecutorContext_System_Threading_CancellationToken_"></a>

### RunStepAsync(StepExecutorContext, CancellationToken)


```csharp
Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken)
```


Runs one step. &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; ends when the test is cancelled, when the step deadline
passes, or on any other hard stop. Stop promptly: the runtime settles the step at the hard stop either way, so
a late verdict is never trusted over it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;StepVerdict&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
