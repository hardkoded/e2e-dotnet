---
title: "Report"
description: "The report-1 document, the shape Report() returns and report.json holds (upstream's Report, an alias of Report1Document, report/build.ts). A report is a reco..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Report"
  hidden: true
---

<a id="E2E_Reports_Report"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record Report : IEquatable<Report>
```


The report-1 document, the shape [Report()](/api/E2E/E2ESession/#E2E_E2ESession_Report) returns and `report.json` holds (upstream's `Report`, an alias of
`Report1Document`, `report/build.ts`). A report is a record of what happened. Changing it changes nothing, and a caller that
wants the document as JSON reads `report.json`. The port has no serial groups, so the document has none (its `serialGroups` is always empty, and so is a carried block's).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Report&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs)

## Properties

<a id="E2E_Reports_Report_Run_"></a>
<a id="E2E_Reports_Report_Run"></a>

### Run


```csharp
public ReportRun Run { get; init; }
```


The run the document tells.

**Value:** [ReportRun](/api/E2E.Reports/ReportRun/)

<a id="E2E_Reports_Report_SchemaVersion_"></a>
<a id="E2E_Reports_Report_SchemaVersion"></a>

### SchemaVersion


```csharp
public string SchemaVersion { get; init; }
```


Always `report-1`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
