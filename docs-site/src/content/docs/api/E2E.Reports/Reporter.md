---
title: "Reporter"
description: "A reporter, registered with E2EReporterAttribute (upstream's Reporter, types.ts). OnRunFinishedAsync runs once, when the test run is over and report.json is..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Reporter"
  hidden: true
---

<a id="E2E_Reports_Reporter"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public abstract class Reporter
```


A reporter, registered with [E2EReporterAttribute](/api/E2E.Reports/E2EReporterAttribute/) (upstream's `Reporter`, `types.ts`). `OnRunFinishedAsync` runs
once, when the test run is over and `report.json` is final; the run is awaited for one minute, and the rows it returns print under the run's
output. The token is cancelled when that budget runs out or the run is forced to stop, so a reporter hands it to its requests and leaves nothing
running behind. A reporter can never change the run's outcome: a failure or a timeout is one line on stderr. The test run ends when the
run fixture (`E2ERunFixture` of `E2E.NUnit` or `E2E.XUnit.V3`) is torn down, so a run without it calls no reporter. A reporter
has a public constructor with no parameters and no side effects. Not ported: `onEvent`, since the port emits no run events.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Properties

<a id="E2E_Reports_Reporter_Name_"></a>
<a id="E2E_Reports_Reporter_Name"></a>

### Name


```csharp
public abstract string Name { get; }
```


Label in diagnostics.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Reports_Reporter_OnRunFinishedAsync_"></a>
<a id="E2E_Reports_Reporter_OnRunFinishedAsync_E2E_Reports_FinishedRun_System_Threading_CancellationToken_"></a>

### OnRunFinishedAsync(FinishedRun, CancellationToken)


```csharp
public virtual Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
```


Runs once the report is written; the returned rows print under the run's output. Null prints nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `run` | [FinishedRun](/api/E2E.Reports/FinishedRun/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;ReporterSummaryRow&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
