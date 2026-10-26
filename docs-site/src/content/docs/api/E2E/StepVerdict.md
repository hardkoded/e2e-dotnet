---
title: "StepVerdict"
description: "Upstream's StepVerdict: how an executor ended the step. Failed means the app did not behave as the step required. Blocked means the environment, the credenti..."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepVerdict"
  hidden: true
---

<a id="E2E_StepVerdict"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public readonly record struct StepVerdict : IEquatable<StepVerdict>
```


Upstream's `StepVerdict`: how an executor ended the step. [Failed](/api/E2E/StepVerdictStatus/#E2E_StepVerdictStatus_Failed) means the app did not
behave as the step required. [Blocked](/api/E2E/StepVerdictStatus/#E2E_StepVerdictStatus_Blocked) means the environment, the credentials, or the
executor's own budget prevented a verdict about the app, and it always carries a blockable code.

**Implements:** [IEquatable&lt;StepVerdict&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L548)

## Constructors

<a id="E2E_StepVerdict__ctor_"></a>
<a id="E2E_StepVerdict__ctor_E2E_StepVerdictStatus_System_String_System_String_"></a>

### StepVerdict(StepVerdictStatus, string, string?)


```csharp
public StepVerdict(StepVerdictStatus Status, string Summary, string? ErrorCode = null)
```


Upstream's `StepVerdict`: how an executor ended the step. [Failed](/api/E2E/StepVerdictStatus/#E2E_StepVerdictStatus_Failed) means the app did not
behave as the step required. [Blocked](/api/E2E/StepVerdictStatus/#E2E_StepVerdictStatus_Blocked) means the environment, the credentials, or the
executor's own budget prevented a verdict about the app, and it always carries a blockable code.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Status` | [StepVerdictStatus](/api/E2E/StepVerdictStatus/) | How the step ended. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What happened, in a sentence or two. Never empty. |
| `ErrorCode` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The agent error code, such as `ASSERTION_FAILED`. A passed verdict carries none. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L548)

## Properties

<a id="E2E_StepVerdict_ErrorCode_"></a>
<a id="E2E_StepVerdict_ErrorCode"></a>

### ErrorCode


```csharp
public string? ErrorCode { get; init; }
```


The agent error code, such as `ASSERTION_FAILED`. A passed verdict carries none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L548)

<a id="E2E_StepVerdict_Status_"></a>
<a id="E2E_StepVerdict_Status"></a>

### Status


```csharp
public StepVerdictStatus Status { get; init; }
```


How the step ended.

**Value:** [StepVerdictStatus](/api/E2E/StepVerdictStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L548)

<a id="E2E_StepVerdict_Summary_"></a>
<a id="E2E_StepVerdict_Summary"></a>

### Summary


```csharp
public string Summary { get; init; }
```


What happened, in a sentence or two. Never empty.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L548)
