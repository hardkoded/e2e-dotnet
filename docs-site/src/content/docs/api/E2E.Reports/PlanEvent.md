---
title: "PlanEvent"
description: "Upstream's plan fact: what the run will execute, emitted once collection, selection, and every target's setup are done. From here the run executes, so the li..."
editUrl: false
prev: false
next: false
sidebar:
  label: "PlanEvent"
  hidden: true
---

<a id="E2E_Reports_PlanEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record PlanEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<PlanEvent>
```


Upstream's `plan` fact: what the run will execute, emitted once collection, selection, and every target's setup are done.
From here the run executes, so the list reporter's clock starts with it. `Files` is upstream's inline `{ file, target, tests }` list, one tuple per test file on one target.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;PlanEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_PlanEvent__ctor_"></a>
<a id="E2E_Reports_PlanEvent__ctor_System_Int32_System_Collections_Generic_IReadOnlyList_System_ValueTuple_System_String_System_String_System_Int32___"></a>

### `PlanEvent(int, IReadOnlyList<(string File, string Target, int Tests)>)`


```csharp
public PlanEvent(int Total, IReadOnlyList<(string File, string Target, int Tests)> Files)
```


Upstream's `plan` fact: what the run will execute, emitted once collection, selection, and every target's setup are done.
From here the run executes, so the list reporter's clock starts with it. `Files` is upstream's inline `{ file, target, tests }` list, one tuple per test file on one target.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Total` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Files` | [IReadOnlyList&lt;(string File, string Target, int Tests)&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Reports_PlanEvent_Files_"></a>
<a id="E2E_Reports_PlanEvent_Files"></a>

### Files


```csharp
public IReadOnlyList<(string File, string Target, int Tests)> Files { get; init; }
```


**Value:** [IReadOnlyList&lt;(string File, string Target, int Tests)&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Reports_PlanEvent_Total_"></a>
<a id="E2E_Reports_PlanEvent_Total"></a>

### Total


```csharp
public int Total { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
