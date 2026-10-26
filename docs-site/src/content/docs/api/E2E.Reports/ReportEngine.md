---
title: "ReportEngine"
description: "The engine a target runs on (upstream's ReportTarget['engine'])."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportEngine"
  hidden: true
---

<a id="E2E_Reports_ReportEngine"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportEngine : IEquatable<ReportEngine>
```


The engine a target runs on (upstream's `ReportTarget['engine']`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportEngine&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L123)

## Properties

<a id="E2E_Reports_ReportEngine_Name_"></a>
<a id="E2E_Reports_ReportEngine_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


`none` for a target without an engine.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L126)

<a id="E2E_Reports_ReportEngine_SpiVersion_"></a>
<a id="E2E_Reports_ReportEngine_SpiVersion"></a>

### SpiVersion


```csharp
public int SpiVersion { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L130)

<a id="E2E_Reports_ReportEngine_Version_"></a>
<a id="E2E_Reports_ReportEngine_Version"></a>

### Version


```csharp
public string Version { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L128)
