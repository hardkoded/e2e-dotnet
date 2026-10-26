---
title: "ReportProject"
description: "The project a run belongs to (upstream's run.project)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportProject"
  hidden: true
---

<a id="E2E_Reports_ReportProject"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportProject : IEquatable<ReportProject>
```


The project a run belongs to (upstream's `run.project`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportProject&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L79)

## Properties

<a id="E2E_Reports_ReportProject_ConfigDigest_"></a>
<a id="E2E_Reports_ReportProject_ConfigDigest"></a>

### ConfigDigest


```csharp
public string ConfigDigest { get; init; }
```


The digest of the resolved config: 64 hexadecimal characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L84)

<a id="E2E_Reports_ReportProject_Id_"></a>
<a id="E2E_Reports_ReportProject_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L81)
