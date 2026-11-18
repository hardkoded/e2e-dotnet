---
title: "JunitReportOptions"
description: "Options for rendering JUnit reports (upstream's JunitReportOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "JunitReportOptions"
  hidden: true
---

<a id="E2E_Reports_JunitReportOptions"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record JunitReportOptions : IEquatable<JunitReportOptions>
```


Options for rendering JUnit reports (upstream's `JunitReportOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;JunitReportOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/JunitReport.cs)

## Properties

<a id="E2E_Reports_JunitReportOptions_ArtifactsDir_"></a>
<a id="E2E_Reports_JunitReportOptions_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public string? ArtifactsDir { get; init; }
```


Where artifact files are located relative to the repository root or CI working directory (such as GitLab's `$CI_PROJECT_DIR`).
When provided, a test with a failure screenshot gains `<system-out>[[ATTACHMENT|<path>]]</system-out>`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
