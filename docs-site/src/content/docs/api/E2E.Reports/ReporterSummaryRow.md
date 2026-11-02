---
title: "ReporterSummaryRow"
description: "One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's ReporterSummary element)."
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
public sealed record ReporterSummaryRow : IEquatable<ReporterSummaryRow>
```


One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's `ReporterSummary` element).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReporterSummaryRow&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Constructors

<a id="E2E_Reports_ReporterSummaryRow__ctor_"></a>
<a id="E2E_Reports_ReporterSummaryRow__ctor_System_String_System_String_"></a>

### ReporterSummaryRow(string, string)


```csharp
public ReporterSummaryRow(string Label, string Text)
```


One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's `ReporterSummary` element).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_ReporterSummaryRow_Label_"></a>
<a id="E2E_Reports_ReporterSummaryRow_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReporterSummaryRow_Text_"></a>
<a id="E2E_Reports_ReporterSummaryRow_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
