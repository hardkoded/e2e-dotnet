---
title: "Budget"
description: "Upstream's budget.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Budget"
  hidden: true
---

<a id="E2E_Decision_Budget"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Budget
```


Upstream's `budget.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Budget.cs)

## Methods

<a id="E2E_Decision_Budget_RecordedAsync_"></a>
<a id="E2E_Decision_Budget_RecordedAsync__1_E2E_StepExecutorContext_System_String_System_String_System_Func_System_Threading_Tasks_Task_E2E_Decision_Recorded___0____"></a>

### `RecordedAsync<T>(StepExecutorContext, string, string, Func<Task<Recorded<T>>>)`


```csharp
public static Task<T> RecordedAsync<T>(StepExecutorContext ctx, string provider, string modelId, Func<Task<Recorded<T>>> call)
```


Upstream's `recorded`. Runs one model call against the step budget: cancelled before it starts or after it returns, and
recorded with its timing and tokens even when it throws. Errors propagate untouched for the caller to map.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `call` | [Func&lt;Task&lt;Recorded&lt;T&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Budget_RecordedAsync__1_E2E_StepExecutorContext_System_String_System_String_System_Threading_CancellationToken_System_Func_System_Threading_Tasks_Task_E2E_Decision_Recorded___0____"></a>

### `RecordedAsync<T>(StepExecutorContext, string, string, CancellationToken, Func<Task<Recorded<T>>>)`


```csharp
public static Task<T> RecordedAsync<T>(StepExecutorContext ctx, string provider, string modelId, CancellationToken signal, Func<Task<Recorded<T>>> call)
```


`RecordedAsync&lt;T&gt;` under &lt;code class="paramref"&gt;signal&lt;/code&gt;: the
attempt's token linked with the caller's (.NET-only: the runtime hands an executor both).

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `signal` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `call` | [Func&lt;Task&lt;Recorded&lt;T&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
