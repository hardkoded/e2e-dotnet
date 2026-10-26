---
title: "ReportRunner"
description: "The runner that made a report (upstream's run.runner)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportRunner"
  hidden: true
---

<a id="E2E_Reports_ReportRunner"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportRunner : IEquatable<ReportRunner>
```


The runner that made a report (upstream's `run.runner`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportRunner&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L71)

## Properties

<a id="E2E_Reports_ReportRunner_Name_"></a>
<a id="E2E_Reports_ReportRunner_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L73)

<a id="E2E_Reports_ReportRunner_Version_"></a>
<a id="E2E_Reports_ReportRunner_Version"></a>

### Version


```csharp
public string Version { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L75)
