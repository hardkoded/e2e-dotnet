---
title: "StepProgressIdentity"
description: "Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's StepProgressIdentity)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepProgressIdentity"
  hidden: true
---

<a id="E2E_Reports_StepProgressIdentity"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepProgressIdentity : IEquatable<StepProgressIdentity>
```


Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's `StepProgressIdentity`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepProgressIdentity&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs)

## Constructors

<a id="E2E_Reports_StepProgressIdentity__ctor_"></a>
<a id="E2E_Reports_StepProgressIdentity__ctor_System_String_System_Int32_System_String_System_Int32_System_String_System_String_"></a>

### StepProgressIdentity(string, int, string, int, string?, string?)


```csharp
public StepProgressIdentity(string AttemptId, int AttemptIndex, string StepId, int StepIndex, string? Actor = null, string? ParentStepId = null)
```


Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's `StepProgressIdentity`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `AttemptId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The attempt the step is of. |
| `AttemptIndex` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The attempt's index. |
| `StepId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The step's id. |
| `StepIndex` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The step's index. |
| `Actor` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The session the step ran on when it is not the test's own, such as `launch-1`; absent for the test's own session. |
| `ParentStepId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The id of the `Step` the step runs inside; absent for a step outside one. |

## Properties

<a id="E2E_Reports_StepProgressIdentity_Actor_"></a>
<a id="E2E_Reports_StepProgressIdentity_Actor"></a>

### Actor


```csharp
public string? Actor { get; init; }
```


The session the step ran on when it is not the test's own, such as `launch-1`; absent for the test's own session.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepProgressIdentity_AttemptId_"></a>
<a id="E2E_Reports_StepProgressIdentity_AttemptId"></a>

### AttemptId


```csharp
public string AttemptId { get; init; }
```


The attempt the step is of.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepProgressIdentity_AttemptIndex_"></a>
<a id="E2E_Reports_StepProgressIdentity_AttemptIndex"></a>

### AttemptIndex


```csharp
public int AttemptIndex { get; init; }
```


The attempt's index.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_StepProgressIdentity_ParentStepId_"></a>
<a id="E2E_Reports_StepProgressIdentity_ParentStepId"></a>

### ParentStepId


```csharp
public string? ParentStepId { get; init; }
```


The id of the `Step` the step runs inside; absent for a step outside one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepProgressIdentity_StepId_"></a>
<a id="E2E_Reports_StepProgressIdentity_StepId"></a>

### StepId


```csharp
public string StepId { get; init; }
```


The step's id.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepProgressIdentity_StepIndex_"></a>
<a id="E2E_Reports_StepProgressIdentity_StepIndex"></a>

### StepIndex


```csharp
public int StepIndex { get; init; }
```


The step's index.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
