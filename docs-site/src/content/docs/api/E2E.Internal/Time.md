---
title: "Time"
description: "Deadline and polling helpers, as upstream's internal/time.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Time"
  hidden: true
---

<a id="E2E_Internal_Time"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public static class Time
```


Deadline and polling helpers, as upstream's `internal/time.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Internal/Time.cs)

## Fields

<a id="E2E_Internal_Time_NegationGrace"></a>

### NegationGrace


```csharp
public static readonly TimeSpan NegationGrace
```


How long a negated assertion must hold before it passes.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Internal_Time_Never"></a>

### Never


```csharp
public static readonly DateTimeOffset Never
```


The deadline of a step that has none of its own and no running phase to end it: far enough that nothing reaches it, and not
[MaxValue](https://learn.microsoft.com/dotnet/api/system.datetimeoffset.maxvalue), so adding a read's time to it cannot overflow.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Internal_Time_PollInterval"></a>

### PollInterval


```csharp
public static readonly TimeSpan PollInterval
```


Canonical polling cadence for locator and assertion loops.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Internal_Time_Unbounded"></a>

### Unbounded


```csharp
public static readonly TimeSpan Unbounded
```


The longest budget handed to an engine read or a timer: a day, standing for no limit when nothing caps a step.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

## Methods

<a id="E2E_Internal_Time_Between_"></a>
<a id="E2E_Internal_Time_Between_System_TimeSpan_System_TimeSpan_System_TimeSpan_"></a>

### Between(TimeSpan, TimeSpan, TimeSpan)


```csharp
public static TimeSpan Between(TimeSpan value, TimeSpan least, TimeSpan most)
```


&lt;code class="paramref"&gt;value&lt;/code&gt; held between &lt;code class="paramref"&gt;least&lt;/code&gt; and &lt;code class="paramref"&gt;most&lt;/code&gt; (.NET-only helper for upstream's `Math.max(1, ms)`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `least` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `most` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Internal_Time_BudgetText_"></a>
<a id="E2E_Internal_Time_BudgetText_System_Int64_"></a>

### BudgetText(long)


```csharp
public static string BudgetText(long ms)
```


A budget as a message names it, exactly, in the largest unit that divides it: `5m`, `30s`, `1500ms` (upstream's `budgetText`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ms` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_Time_CheckStepTimeout_"></a>
<a id="E2E_Internal_Time_CheckStepTimeout_System_Nullable_System_TimeSpan__System_String_"></a>

### CheckStepTimeout(TimeSpan?, string)


```csharp
public static void CheckStepTimeout(TimeSpan? timeout, string api)
```


Refuses a per-call step timeout below zero as `INVALID_ARGUMENT` (upstream's `checkStepTimeout`; a [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) is
always a finite number of milliseconds, so only the sign is left to check). Zero means no step deadline: the step waits until the
running phase's deadline (the test timeout in the body).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `api` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_Internal_Time_IsOperationTimeout_"></a>
<a id="E2E_Internal_Time_IsOperationTimeout_System_Exception_"></a>

### IsOperationTimeout(Exception)


```csharp
public static bool IsOperationTimeout(Exception cause)
```


Whether &lt;code class="paramref"&gt;cause&lt;/code&gt; is the engine's `OPERATION_TIMEOUT`, bare or wrapped by the locator failure it was translated into (upstream's `isOperationTimeout`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_Time_Link_"></a>
<a id="E2E_Internal_Time_Link_System_Threading_CancellationToken_System_Threading_CancellationToken_"></a>

### Link(CancellationToken, CancellationToken)


```csharp
public static CancellationToken Link(CancellationToken caller, CancellationToken session)
```


The token a call runs under: the caller's token, ended also by the session's. No source is made when one token
already stands for both, so a call does not leave a registration on the session token. When both can cancel
and differ, the linked source lives until either token ends.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `caller` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `session` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

<a id="E2E_Internal_Time_PollConditionAsync_"></a>
<a id="E2E_Internal_Time_PollConditionAsync_E2E_Internal_PollConditionOptions_"></a>

### PollConditionAsync(PollConditionOptions)


```csharp
public static Task PollConditionAsync(PollConditionOptions options)
```


Polls a condition until it holds (or, when negated, until its negation has held continuously for the negation
grace window), throwing the caller's error at the deadline. No sleep runs past the deadline, so the last read
starts at it and never sees a state that arrived later.
A negation holds from the moment the read that first saw it was issued, so a slow read counts toward the
window. A budget shorter than the window still has to be satisfiable: the negation then only needs to hold for
the whole budget, and passes at the deadline on what it has seen, since a read past the deadline has no budget
left. A read the deadline cut off (see [CutOff(Exception)](https://learn.microsoft.com/dotnet/api/system.exception)) after an earlier one completed saw
nothing: the poll ends there, and a negation passes if it had held long enough by then.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [PollConditionOptions](/api/E2E.Internal/PollConditionOptions/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Internal_Time_Sleep_"></a>
<a id="E2E_Internal_Time_Sleep_System_TimeSpan_System_TimeProvider_System_Threading_CancellationToken_"></a>

### Sleep(TimeSpan, TimeProvider, CancellationToken)


```csharp
public static Task Sleep(TimeSpan duration, TimeProvider clock, CancellationToken cancellationToken = default)
```


Waits for &lt;code class="paramref"&gt;duration&lt;/code&gt; on &lt;code class="paramref"&gt;clock&lt;/code&gt; (upstream's `sleep`). A cancel rejects with `CANCELLED`,
immediately when it was already cancelled.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `duration` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `clock` | [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Internal_Time_StepBudget_"></a>
<a id="E2E_Internal_Time_StepBudget_System_Nullable_System_TimeSpan__System_String_System_TimeSpan_E2E_Internal_Deadline_System_TimeProvider_"></a>

### StepBudget(TimeSpan?, string, TimeSpan, Deadline?, TimeProvider)


```csharp
public static TimeSpan StepBudget(TimeSpan? timeout, string api, TimeSpan fallback, Deadline? phase, TimeProvider clock)
```


`StepDeadline` as a budget, for a call that hands one to the engine: what is left of the deadline, at least 1 ms and
at most [Unbounded](#E2E_Internal_Time_Unbounded).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `api` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `fallback` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `phase` | [Deadline](/api/E2E.Internal/Deadline/) |  |
| `clock` | [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider) |  |

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Internal_Time_StepDeadline_"></a>
<a id="E2E_Internal_Time_StepDeadline_System_Nullable_System_TimeSpan__System_String_System_TimeSpan_E2E_Internal_Deadline_System_TimeProvider_"></a>

### StepDeadline(TimeSpan?, string, TimeSpan, Deadline?, TimeProvider)


```csharp
public static Deadline StepDeadline(TimeSpan? timeout, string api, TimeSpan fallback, Deadline? phase, TimeProvider clock)
```


The deadline of a step on a call's own &lt;code class="paramref"&gt;timeout&lt;/code&gt; (upstream's `stepTimeout`, capped by the phase as
`Deadline.min` does): the timeout, or &lt;code class="paramref"&gt;fallback&lt;/code&gt; when none is given, from now. Zero sets no deadline of its
own, so &lt;code class="paramref"&gt;phase&lt;/code&gt; alone bounds it; with no phase it is [Never](#E2E_Internal_Time_Never).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `api` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `fallback` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `phase` | [Deadline](/api/E2E.Internal/Deadline/) |  |
| `clock` | [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider) |  |

**Returns:** [Deadline](/api/E2E.Internal/Deadline/)

<a id="E2E_Internal_Time_WithTimeout_"></a>
<a id="E2E_Internal_Time_WithTimeout__1_System_Threading_Tasks_Task___0__System_TimeSpan_System_Func_System_Exception__"></a>

### `WithTimeout<T>(Task<T>, TimeSpan, Func<Exception>)`


```csharp
public static Task<T> WithTimeout<T>(Task<T> work, TimeSpan timeout, Func<Exception> onTimeout)
```


Races &lt;code class="paramref"&gt;work&lt;/code&gt; against a timeout; on timeout throws what &lt;code class="paramref"&gt;onTimeout&lt;/code&gt; builds (upstream's
`withTimeout`). An infinite timeout sets no timer.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `work` | [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `onTimeout` | [Func&lt;Exception&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Internal_Time_WithinCleanupBudget_"></a>
<a id="E2E_Internal_Time_WithinCleanupBudget_System_Threading_Tasks_Task_System_Threading_CancellationToken_System_TimeSpan_"></a>

### WithinCleanupBudget(Task, CancellationToken, TimeSpan)


```csharp
public static Task WithinCleanupBudget(Task work, CancellationToken abort, TimeSpan timeout)
```


Best-effort cleanup (upstream's `withinCleanupBudget`): returns when &lt;code class="paramref"&gt;work&lt;/code&gt; settles, when &lt;code class="paramref"&gt;abort&lt;/code&gt;
fires, or after &lt;code class="paramref"&gt;timeout&lt;/code&gt;, and absorbs the work's failure, so a cleanup never fails the attempt it closes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `work` | [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) |  |
| `abort` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
