---
title: "IExecutorBudgets"
description: "Step budgets, read and reported by the executor and enforced by the runtime."
editUrl: false
prev: false
next: false
sidebar:
  label: "IExecutorBudgets"
  hidden: true
---

<a id="E2E_IExecutorBudgets"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IExecutorBudgets
```


Step budgets, read and reported by the executor and enforced by the runtime.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Properties

<a id="E2E_IExecutorBudgets_MaxActions_"></a>
<a id="E2E_IExecutorBudgets_MaxActions"></a>

### MaxActions


```csharp
int MaxActions { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_IExecutorBudgets_MaxModelCalls_"></a>
<a id="E2E_IExecutorBudgets_MaxModelCalls"></a>

### MaxModelCalls


```csharp
int MaxModelCalls { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

<a id="E2E_IExecutorBudgets_ActionsUsed_"></a>
<a id="E2E_IExecutorBudgets_ActionsUsed"></a>

### ActionsUsed()


```csharp
int ActionsUsed()
```


The action slots the step has used, replayed and live actions alike.

**Returns:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_IExecutorBudgets_RecordModelCall_"></a>
<a id="E2E_IExecutorBudgets_RecordModelCall_E2E_ExecutorModelCall_"></a>

### RecordModelCall(ExecutorModelCall?)


```csharp
void RecordModelCall(ExecutorModelCall? usage = null)
```


Records one model call the executor made. Its usage feeds the step's model call and token counts. Throws
`STEP_BUDGET_EXHAUSTED` once the call count passes [MaxModelCalls](#E2E_IExecutorBudgets_MaxModelCalls): the budget is enforced, not advisory.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `usage` | [ExecutorModelCall](/api/E2E/ExecutorModelCall/) |  |

<a id="E2E_IExecutorBudgets_RemainingMs_"></a>
<a id="E2E_IExecutorBudgets_RemainingMs"></a>

### RemainingMs()


```csharp
int RemainingMs()
```


The milliseconds left of the step's deadline.

**Returns:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_IExecutorBudgets_RunToolAsync_"></a>
<a id="E2E_IExecutorBudgets_RunToolAsync__1_System_String_System_Boolean_System_Func_System_Threading_Tasks_Task___0___"></a>

### `RunToolAsync<T>(string, bool, Func<Task<T>>)`


```csharp
Task<T> RunToolAsync<T>(string name, bool mutates, Func<Task<T>> body)
```


Runs one project tool under the step's accounting. A mutating tool reserves an action slot before its body runs, and may
throw `STEP_BUDGET_EXHAUSTED`. It ends replay of the step, since the grammar cannot reproduce it.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `mutates` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `body` | [Func&lt;Task&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
