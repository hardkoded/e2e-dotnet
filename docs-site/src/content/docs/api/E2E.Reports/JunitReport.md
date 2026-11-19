---
title: "JunitReport"
description: "The JUnit XML renderer (upstream's report/junit.ts): the report as junit.xml, for CI test summaries. Like every reporter it reads the document, never runner..."
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


The JUnit XML renderer (upstream's `report/junit.ts`): the report as `junit.xml`, for CI test summaries. Like every reporter it
reads the document, never runner state, and it can never change run status.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Reports_JunitReport_RenderJunitReport_"></a>
<a id="E2E_Reports_JunitReport_RenderJunitReport_E2E_Reports_Report_E2E_Reports_JunitReportOptions_"></a>

### RenderJunitReport(Report, JunitReportOptions?)


```csharp
public static string RenderJunitReport(Report report, JunitReportOptions? options = null)
```


Renders the report-1 document as JUnit XML: one `<testsuite>` per test file (in report order, so the output is completion-time
independent), one `<testcase>` per test-target pair, plus a `run` suite for run-level errors. A pair a test filter left
out is a skipped case naming the filter, so a run the filters left empty still lists its suite. Upstream's `renderJunitReport`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `report` | [Report](/api/E2E.Reports/Report/) | The report a reporter receives ([Report](/api/E2E.Reports/FinishedRun/#E2E_Reports_FinishedRun_Report)). |
| `options` | [JunitReportOptions](/api/E2E.Reports/JunitReportOptions/) | What attachments are relative to; &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; for none. |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — The XML document, ending in a newline.
