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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L30)

## Constructors

<a id="E2E_Reports_StepProgressIdentity__ctor_"></a>
<a id="E2E_Reports_StepProgressIdentity__ctor_System_String_System_Int32_System_String_System_Int32_"></a>

### StepProgressIdentity(string, int, string, int)


```csharp
public StepProgressIdentity(string AttemptId, int AttemptIndex, string StepId, int StepIndex)
```


Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's `StepProgressIdentity`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `AttemptId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `AttemptIndex` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `StepId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `StepIndex` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L30)

## Properties

<a id="E2E_Reports_StepProgressIdentity_AttemptId_"></a>
<a id="E2E_Reports_StepProgressIdentity_AttemptId"></a>

### AttemptId


```csharp
public string AttemptId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L30)

<a id="E2E_Reports_StepProgressIdentity_AttemptIndex_"></a>
<a id="E2E_Reports_StepProgressIdentity_AttemptIndex"></a>

### AttemptIndex


```csharp
public int AttemptIndex { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L30)

<a id="E2E_Reports_StepProgressIdentity_StepId_"></a>
<a id="E2E_Reports_StepProgressIdentity_StepId"></a>

### StepId


```csharp
public string StepId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L30)

<a id="E2E_Reports_StepProgressIdentity_StepIndex_"></a>
<a id="E2E_Reports_StepProgressIdentity_StepIndex"></a>

### StepIndex


```csharp
public int StepIndex { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/StepProgress.cs#L30)
