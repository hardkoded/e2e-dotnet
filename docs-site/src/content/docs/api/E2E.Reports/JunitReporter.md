---
title: "JunitReporter"
description: "The built-in junit reporter (upstream's junitReporter): the report as JUnit XML in junit.xml beside report.json, for CI test summaries. List it in reporters..."
editUrl: false
prev: false
next: false
sidebar:
  label: "JunitReporter"
  hidden: true
---

<a id="E2E_Reports_JunitReporter"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed class JunitReporter : Reporter
```


The built-in `junit` reporter (upstream's `junitReporter`): the report as JUnit XML in `junit.xml` beside `report.json`, for CI
test summaries. List it in `reporters` of `e2e.config.json` by its type name.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Reporter](/api/E2E.Reports/Reporter/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/JunitReport.cs)

## Properties

<a id="E2E_Reports_JunitReporter_Name_"></a>
<a id="E2E_Reports_JunitReporter_Name"></a>

### Name


```csharp
public override string Name { get; }
```


Label in diagnostics.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Reports_JunitReporter_OnRunFinishedAsync_"></a>
<a id="E2E_Reports_JunitReporter_OnRunFinishedAsync_E2E_Reports_FinishedRun_System_Threading_CancellationToken_"></a>

### OnRunFinishedAsync(FinishedRun, CancellationToken)


```csharp
public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
```


Runs once the report is written; the returned rows print under the run's output. Null prints nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `run` | [FinishedRun](/api/E2E.Reports/FinishedRun/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;ReporterSummaryRow&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
