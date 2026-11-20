---
title: "JunitReport"
description: "JUnit XML: the report-1 document as junit.xml (upstream's report/junit.ts). RenderJunitReport renders one <testsuite> per test file (in report order, so the..."
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


JUnit XML: the report-1 document as `junit.xml` (upstream's `report/junit.ts`). `RenderJunitReport` renders one
`<testsuite>` per test file (in report order, so the output is completion-time independent), one `<testcase>` per
test-target pair, plus a `run` suite for run-level errors. A pair a test filter left out is a skipped case naming the filter, so a run
the filters left empty still lists its suite. Like every reporter it reads the document, never runner state, and it can never change run status.
The port has no serial groups, so a result always answers from its own attempts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Reports_JunitReport_RenderJunitReport_"></a>
<a id="E2E_Reports_JunitReport_RenderJunitReport_E2E_Reports_Report_E2E_Reports_JunitReportOptions_"></a>

### RenderJunitReport(Report, JunitReportOptions?)


```csharp
public static string RenderJunitReport(Report report, JunitReportOptions? options = null)
```


Renders the report-1 document as JUnit XML: one `<testsuite>` per test file (in report order), one `<testcase>` per test-target
pair, plus a `run` suite for run-level errors. Upstream's `renderJunitReport`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `report` | [Report](/api/E2E.Reports/Report/) |  |
| `options` | [JunitReportOptions](/api/E2E.Reports/JunitReportOptions/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
