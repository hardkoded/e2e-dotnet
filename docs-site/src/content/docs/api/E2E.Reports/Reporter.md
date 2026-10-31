---
title: "Reporter"
description: "A reporter, registered with E2EReporterAttribute (upstream's `Reporter`, `types.ts`). `OnRunFinishedAsync` runs once, when the test run is over and `report.json` is final, and is awaited for one minute; the rows it returns print after the tests. A reporter can never change the run's outcome: a failure or a timeout is one line on stderr. Not ported: `onEvent`."
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


A reporter, registered with E2EReporterAttribute (upstream's `Reporter`, `types.ts`). `OnRunFinishedAsync` runs once, when the test run is over and `report.json` is final, and is awaited for one minute; the rows it returns print after the tests. A reporter can never change the run's outcome: a failure or a timeout is one line on stderr. Not ported: `onEvent`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs#L32)

## Properties

### Name


```csharp
public abstract string Name { get; }
```


Label in diagnostics.

## Methods

### OnRunFinishedAsync(FinishedRun, CancellationToken)


```csharp
public virtual Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
```


Runs once the report is written; the returned rows print under the run's output. Null prints nothing. The token is cancelled when the one-minute budget runs out or the run is forced to stop.
