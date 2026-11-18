---
title: "JunitReport"
description: "JUnit XML reporter: the report-1 document as junit.xml (upstream's report/junit.ts). One <testsuite> per test file (in report order, so the output is complet..."
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


JUnit XML reporter: the report-1 document as `junit.xml` (upstream's `report/junit.ts`). One `<testsuite>` per test file (in report
order, so the output is completion-time independent), one `<testcase>` per test-target pair, plus a `run` suite for run-level errors.
A pair a test filter left out is a skipped case naming the filter. Like every reporter it reads the document, never runner state, and it can never
change run status. Not ported, since the report has no such records: serial groups and the `test.fail` expected-failure note.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Reports_JunitReport_RenderJunitReport_"></a>
<a id="E2E_Reports_JunitReport_RenderJunitReport_E2E_Reports_Report_E2E_Reports_JunitReportOptions_"></a>

### RenderJunitReport(Report, JunitReportOptions?)


```csharp
public static string RenderJunitReport(Report report, JunitReportOptions? options = null)
```


Renders the report-1 document as JUnit XML (upstream's `renderJunitReport`). A test the selection did not choose is left out, unless a test
filter did: that one is a skipped case.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `report` | [Report](/api/E2E.Reports/Report/) |  |
| `options` | [JunitReportOptions](/api/E2E.Reports/JunitReportOptions/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
