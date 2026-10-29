---
title: "ExploreOutcome"
description: "What an exploration ended with. Upstream's ExploreOutcome, which extends the runner's RunOutcome."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreOutcome"
  hidden: true
---

<a id="E2E_ExploreOutcome"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExploreOutcome : IEquatable<ExploreOutcome>
```


What an exploration ended with. Upstream's `ExploreOutcome`, which extends the runner's `RunOutcome`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExploreOutcome&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L113)

## Constructors

<a id="E2E_ExploreOutcome__ctor_"></a>
<a id="E2E_ExploreOutcome__ctor_E2E_RunStatus_System_Int32_E2E_E2EException_E2E_ReportExplore_"></a>

### ExploreOutcome(RunStatus, int, E2EException?, ReportExplore)


```csharp
public ExploreOutcome(RunStatus Status, int ExitCode, E2EException? Error, ReportExplore Explore)
```


What an exploration ended with. Upstream's `ExploreOutcome`, which extends the runner's `RunOutcome`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Status` | [RunStatus](/api/E2E/RunStatus/) | How the run ended. An agent step the run could not finish for lack of credentials, environment, or setup is [Blocked](/api/E2E/RunStatus/#E2E_RunStatus_Blocked). |
| `ExitCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | 0 when the exploration reported no issue (warnings do not count), 1 when it reported one or explored nothing, 2 for a configuration or policy error, 3 for an engine, app, or model provider failure. A failure of closing the exploration joins it: the code is the verdict's combined with the cleanup failure's (precedence 130, 4, 3, 2, 1, 0). |
| `Error` | [E2EException](/api/E2E/E2EException/) | The error that ended the run, or else the first cleanup failure, or null for a pass. |
| `Explore` | [ReportExplore](/api/E2E/ReportExplore/) | What was asked, did, and found. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L113)

## Properties

<a id="E2E_ExploreOutcome_Error_"></a>
<a id="E2E_ExploreOutcome_Error"></a>

### Error


```csharp
public E2EException? Error { get; init; }
```


The error that ended the run, or else the first cleanup failure, or null for a pass.

**Value:** [E2EException](/api/E2E/E2EException/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L113)

<a id="E2E_ExploreOutcome_ExitCode_"></a>
<a id="E2E_ExploreOutcome_ExitCode"></a>

### ExitCode


```csharp
public int ExitCode { get; init; }
```


0 when the exploration reported no issue (warnings do not count), 1 when it reported one or explored nothing, 2 for a configuration or policy error, 3 for an engine, app, or model provider failure. A failure of closing the exploration joins it: the code is the verdict's combined with the cleanup failure's (precedence 130, 4, 3, 2, 1, 0).

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L113)

<a id="E2E_ExploreOutcome_Explore_"></a>
<a id="E2E_ExploreOutcome_Explore"></a>

### Explore


```csharp
public ReportExplore Explore { get; init; }
```


What was asked, did, and found.

**Value:** [ReportExplore](/api/E2E/ReportExplore/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L113)

<a id="E2E_ExploreOutcome_Status_"></a>
<a id="E2E_ExploreOutcome_Status"></a>

### Status


```csharp
public RunStatus Status { get; init; }
```


How the run ended. An agent step the run could not finish for lack of credentials, environment, or setup is [Blocked](/api/E2E/RunStatus/#E2E_RunStatus_Blocked).

**Value:** [RunStatus](/api/E2E/RunStatus/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L113)
