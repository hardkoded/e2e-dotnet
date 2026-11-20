---
title: "JunitReporterOptions"
description: "Options of the built-in JunitReporter (upstream's JunitReporterOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "JunitReporterOptions"
  hidden: true
---

<a id="E2E_Reports_JunitReporterOptions"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record JunitReporterOptions : IEquatable<JunitReporterOptions>
```


Options of the built-in [JunitReporter](/api/E2E.Reports/JunitReporter/) (upstream's `JunitReporterOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;JunitReporterOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/JunitReport.cs)

## Properties

<a id="E2E_Reports_JunitReporterOptions_OutputFile_"></a>
<a id="E2E_Reports_JunitReporterOptions_OutputFile"></a>

### OutputFile


```csharp
public string? OutputFile { get; init; }
```


Where the XML goes, from the project root; `junit.xml` in the output directory by default.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
