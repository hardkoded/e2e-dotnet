---
title: "Timing"
description: "Cancellation with the engine error taxonomy (upstream engine/timing.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Timing"
  hidden: true
---

<a id="E2E_Engine_Timing"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class Timing
```


Cancellation with the engine error taxonomy (upstream `engine/timing.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Timing.cs)

## Methods

<a id="E2E_Engine_Timing_RaceAbortAsync_"></a>
<a id="E2E_Engine_Timing_RaceAbortAsync_System_Func_System_Threading_Tasks_Task__System_Threading_CancellationToken_System_String_"></a>

### `RaceAbortAsync(Func<Task>, CancellationToken, string)`


```csharp
public static Task RaceAbortAsync(Func<Task> work, CancellationToken cancellationToken, string label)
```


Checks cancellation before dispatching the work, then abandons the wait on abort. The work an abort abandons keeps running,
and its late failure is absorbed so it never surfaces as an unobserved exception.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `work` | [Func&lt;Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_Timing_RaceAbortAsync__1_System_Func_System_Threading_Tasks_Task___0___System_Threading_CancellationToken_System_String_"></a>

### `RaceAbortAsync<T>(Func<Task<T>>, CancellationToken, string)`


```csharp
public static Task<T> RaceAbortAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken, string label)
```


Checks cancellation before dispatching the work, then abandons the wait on abort. The work an abort abandons keeps running,
and its late failure is absorbed so it never surfaces as an unobserved exception.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `work` | [Func&lt;Task&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
