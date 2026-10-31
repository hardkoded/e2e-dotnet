---
title: "ReporterSummaryRow"
description: "One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's `ReporterSummary` element)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReporterSummaryRow"
  hidden: true
---

<a id="E2E_Reports_ReporterSummaryRow"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReporterSummaryRow(string Label, string Text) : IEquatable<ReporterSummaryRow>
```


One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's `ReporterSummary` element).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs#L17)
