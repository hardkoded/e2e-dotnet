---
title: "MarkdownReport"
description: "The report-1 document as one markdown page, laid out for a pull request: a headline with the counts and what the run spent, the run-level errors, one block p..."
editUrl: false
prev: false
next: false
sidebar:
  label: "MarkdownReport"
  hidden: true
---

<a id="E2E_Reports_MarkdownReport"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public static class MarkdownReport
```


The report-1 document as one markdown page, laid out for a pull request: a headline with the counts and what the run spent, the
run-level errors, one block per test that failed with the step it went wrong at and the agent's last word, the flaky tests folded
away with the same block each, what the run's test filters left out, folded away with each reason, and every test folded away under
that as one table, a row per file with its counts and time above the rows of its tests. A green run is the headline, the folds, and
the footer: nothing to read unless the reader opens it. An `e2e explore` run renders its record instead: the goal, every
finding with its evidence, and the assessment. Upstream's `report/markdown.ts`.
Everything a test or the agent wrote (titles, labels, error messages, findings) is untrusted text on its way into markdown a
renderer trusts, so it is escaped and clipped, and the whole body never outgrows what GitHub accepts. The port has no reporter
extension point, so upstream's `markdownReporter` (the `summary.md` writer) is not here.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/MarkdownReport.cs)

## Methods

<a id="E2E_Reports_MarkdownReport_RenderMarkdownReport_"></a>
<a id="E2E_Reports_MarkdownReport_RenderMarkdownReport_E2E_Reports_Report_E2E_Reports_MarkdownReportOptions_"></a>

### RenderMarkdownReport(Report, MarkdownReportOptions?)


```csharp
public static string RenderMarkdownReport(Report report, MarkdownReportOptions? options = null)
```


Renders the run as one markdown page: a pull request comment body, a job summary, or `summary.md`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `report` | [Report](/api/E2E.Reports/Report/) | The report a reporter receives ([Report](/api/E2E.Reports/FinishedRun/#E2E_Reports_FinishedRun_Report)). |
| `options` | [MarkdownReportOptions](/api/E2E.Reports/MarkdownReportOptions/) | What the page links to and how it names itself; &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; for none. |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — The page: markdown, at most about 60,000 characters.
