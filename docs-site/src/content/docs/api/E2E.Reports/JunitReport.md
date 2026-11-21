---
title: "JunitReport"
description: "The Report as JUnit XML (upstream's report/junit.ts, renderJunitReport): one <testsuite> per test file (in report order, so the output is completion-time ind..."
editUrl: false
prev: false
next: false
sidebar:
  label: "JunitReport"
  hidden: true
---

<a id="E2E_Reports_JunitReport"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public static class JunitReport
```


The [Report](/api/E2E.Reports/Report/) as JUnit XML (upstream's `report/junit.ts`, `renderJunitReport`): one `<testsuite>` per test file
(in report order, so the output is completion-time independent), one `<testcase>` per test-target pair, plus a `run` suite
for run-level errors. A pair a test filter left out is a skipped case naming the filter, so a run the filters left empty still lists its
suite. Like every reporter it reads the document, never runner state, and it can never change run status. A reporter that writes the
file renders `FinishedRun.Report` with it (see [JunitReporter](/api/E2E.Reports/JunitReporter/)); the port has no serial groups, so a result always
answers from its own attempts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Reports_JunitReport_RenderJunitReport_"></a>
<a id="E2E_Reports_JunitReport_RenderJunitReport_E2E_Reports_Report_E2E_Reports_JunitReportOptions_"></a>

### RenderJunitReport(Report, JunitReportOptions?)


```csharp
public static string RenderJunitReport(Report report, JunitReportOptions? options = null)
```


Renders the report as JUnit XML. A pair a test filter left out is a skipped case naming the filter; any other result the run did
not select is report-only and left out.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `report` | [Report](/api/E2E.Reports/Report/) | The report a reporter receives ([Report](/api/E2E.Reports/FinishedRun/#E2E_Reports_FinishedRun_Report)). |
| `options` | [JunitReportOptions](/api/E2E.Reports/JunitReportOptions/) | Where the artifacts are, for the failure screenshot links; &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; for none. |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — The XML document, ending in a newline.
