---
title: "ReplayedPrefix"
description: "Upstream's ReplayedPrefix: the mid-step hand-off from a diverged cache replay. The replayed actions already ran against the live app under the same budgets a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReplayedPrefix"
  hidden: true
---

<a id="E2E_ReplayedPrefix"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ReplayedPrefix : IEquatable<ReplayedPrefix>
```


Upstream's `ReplayedPrefix`: the mid-step hand-off from a diverged cache replay. The replayed actions already ran
against the live app under the same budgets and recording as the executor's own. The executor continues the step from
the current state and does not redo them.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReplayedPrefix&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L497)

## Constructors

<a id="E2E_ReplayedPrefix__ctor_"></a>
<a id="E2E_ReplayedPrefix__ctor_System_Collections_Generic_IReadOnlyList_System_String__System_Int32_E2E_ReplayHandOffReason_System_String_"></a>

### `ReplayedPrefix(IReadOnlyList<string>, int, ReplayHandOffReason, string?)`


```csharp
public ReplayedPrefix(IReadOnlyList<string> ReplayedActions, int TotalActions, ReplayHandOffReason StopReason, string? UncertainAction = null)
```


Upstream's `ReplayedPrefix`: the mid-step hand-off from a diverged cache replay. The replayed actions already ran
against the live app under the same budgets and recording as the executor's own. The executor continues the step from
the current state and does not redo them.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ReplayedActions` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | Prose summaries of the actions the replay performed, in order. |
| `TotalActions` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | How many actions the recording holds. |
| `StopReason` | [ReplayHandOffReason](/api/E2E/ReplayHandOffReason/) | Why the replay stopped. |
| `UncertainAction` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Present exactly when &lt;code class="paramref"&gt;StopReason&lt;/code&gt; is [ActionUncertain](/api/E2E/ReplayHandOffReason/#E2E_ReplayHandOffReason_ActionUncertain): the summary of a replayed action whose input may have reached the app although it failed. The executor verifies the state before it tries anything like it again. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L497)

## Properties

<a id="E2E_ReplayedPrefix_ReplayedActions_"></a>
<a id="E2E_ReplayedPrefix_ReplayedActions"></a>

### ReplayedActions


```csharp
public IReadOnlyList<string> ReplayedActions { get; init; }
```


Prose summaries of the actions the replay performed, in order.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L497)

<a id="E2E_ReplayedPrefix_StopReason_"></a>
<a id="E2E_ReplayedPrefix_StopReason"></a>

### StopReason


```csharp
public ReplayHandOffReason StopReason { get; init; }
```


Why the replay stopped.

**Value:** [ReplayHandOffReason](/api/E2E/ReplayHandOffReason/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L497)

<a id="E2E_ReplayedPrefix_TotalActions_"></a>
<a id="E2E_ReplayedPrefix_TotalActions"></a>

### TotalActions


```csharp
public int TotalActions { get; init; }
```


How many actions the recording holds.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L497)

<a id="E2E_ReplayedPrefix_UncertainAction_"></a>
<a id="E2E_ReplayedPrefix_UncertainAction"></a>

### UncertainAction


```csharp
public string? UncertainAction { get; init; }
```


Present exactly when &lt;code class="paramref"&gt;StopReason&lt;/code&gt; is [ActionUncertain](/api/E2E/ReplayHandOffReason/#E2E_ReplayHandOffReason_ActionUncertain): the summary of a
replayed action whose input may have reached the app although it failed. The executor verifies the state before it
tries anything like it again.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L497)
