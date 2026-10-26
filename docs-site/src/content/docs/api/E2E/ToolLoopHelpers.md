---
title: "ToolLoopHelpers"
description: "Loop services for the tool bodies that run work of their own (upstream's ToolLoopHelpers)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ToolLoopHelpers"
  hidden: true
---

<a id="E2E_ToolLoopHelpers"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ToolLoopHelpers
```


Loop services for the tool bodies that run work of their own (upstream's `ToolLoopHelpers`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L92)

## Properties

<a id="E2E_ToolLoopHelpers_Concluding_"></a>
<a id="E2E_ToolLoopHelpers_Concluding"></a>

### Concluding


```csharp
public bool Concluding { get; }
```


True once a verdict or a hard stop landed. A late tool call has nothing left to do.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L104)

## Methods

<a id="E2E_ToolLoopHelpers_GuardAsync_"></a>
<a id="E2E_ToolLoopHelpers_GuardAsync_System_Func_System_Threading_Tasks_Task_System_String___System_String_"></a>

### `GuardAsync(Func<Task<string>>, string?)`


```csharp
public Task<string> GuardAsync(Func<Task<string>> body, string? label = null)
```


Runs one tool body under the loop's policy: nothing runs once the step is concluding, a hard stop of the step ends the
loop, and any other failure goes back to the model as text it can react to, named by &lt;code class="paramref"&gt;label&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `body` | [Func&lt;Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L113)

<a id="E2E_ToolLoopHelpers_ReportHardStop_"></a>
<a id="E2E_ToolLoopHelpers_ReportHardStop_E2E_AgentException_"></a>

### ReportHardStop(AgentException)


```csharp
public void ReportHardStop(AgentException error)
```


Records a hard stop of the step that ends the loop.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [AgentException](/api/E2E/AgentException/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L107)
