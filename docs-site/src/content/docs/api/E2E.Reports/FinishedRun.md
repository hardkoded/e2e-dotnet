---
title: "FinishedRun"
description: "What a reporter receives once the run is over and its report is on disk (upstream's `FinishedRun`, `types.ts`)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FinishedRun"
  hidden: true
---

<a id="E2E_Reports_FinishedRun"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record FinishedRun(Report Report, ReportRunStatus Status, int ExitCode, string ProjectRoot, string ReportPath, string ArtifactsRoot) : IEquatable<FinishedRun>
```


What a reporter receives once the run is over and its report is on disk (upstream's `FinishedRun`, `types.ts`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs#L14)

## Properties

| Property | Meaning |
| --- | --- |
| `Report` | The report-1 document, as written to `ReportPath`. |
| `Status` | The same status `Report.Run.Status` carries. |
| `ExitCode` | The exit code the run ended with. |
| `ProjectRoot` | Absolute project root. |
| `ReportPath` | Where `report.json` was written. |
| `ArtifactsRoot` | Absolute directory the report's artifact paths are relative to: `<output>/results`. |
