---
title: "MarkdownReportOptions"
description: "What a markdown page links to and how it names itself (upstream's MarkdownReportOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "MarkdownReportOptions"
  hidden: true
---

<a id="E2E_Reports_MarkdownReportOptions"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record MarkdownReportOptions : IEquatable<MarkdownReportOptions>
```


What a markdown page links to and how it names itself (upstream's `MarkdownReportOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;MarkdownReportOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/MarkdownReport.cs)

## Properties

<a id="E2E_Reports_MarkdownReportOptions_ArtifactsDir_"></a>
<a id="E2E_Reports_MarkdownReportOptions_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public string? ArtifactsDir { get; init; }
```


The directory the report's artifact paths are relative to, as the reader should see it (`.e2e/results` for a file the project
root is read from). Without [ArtifactsUrl](#E2E_Reports_MarkdownReportOptions_ArtifactsUrl), evidence is listed as paths under it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_MarkdownReportOptions_ArtifactsUrl_"></a>
<a id="E2E_Reports_MarkdownReportOptions_ArtifactsUrl"></a>

### ArtifactsUrl


```csharp
public string? ArtifactsUrl { get; init; }
```


Where the run's artifacts can be fetched: the workflow run page, or a hosted run page. Evidence links there.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_MarkdownReportOptions_SourceUrl_"></a>
<a id="E2E_Reports_MarkdownReportOptions_SourceUrl"></a>

### SourceUrl


```csharp
public Func<string, int, string>? SourceUrl { get; init; }
```


A link to a source line, when the commit is known.

**Value:** [Func&lt;string, int, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Reports_MarkdownReportOptions_Title_"></a>
<a id="E2E_Reports_MarkdownReportOptions_Title"></a>

### Title


```csharp
public string? Title { get; init; }
```


Tells this run's page apart from another's on the same pull request in the headline: `e2e regression: 77 passed`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_MarkdownReportOptions_Traces_"></a>
<a id="E2E_Reports_MarkdownReportOptions_Traces"></a>

### Traces


```csharp
public IReadOnlyDictionary<string, string>? Traces { get; init; }
```


Where each traced result's page is, by result id, as the reader should see the path; the block links there.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
