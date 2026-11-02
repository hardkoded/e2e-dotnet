---
title: "FinishedRun"
description: "What a reporter receives once the run is over and its report is on disk (upstream's FinishedRun, types.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FinishedRun"
  hidden: true
---

<a id="E2E_Reports_FinishedRun"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record FinishedRun : IEquatable<FinishedRun>
```


What a reporter receives once the run is over and its report is on disk (upstream's `FinishedRun`, `types.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;FinishedRun&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Constructors

<a id="E2E_Reports_FinishedRun__ctor_"></a>
<a id="E2E_Reports_FinishedRun__ctor_E2E_Reports_Report_E2E_ReportRunStatus_System_Int32_System_String_System_String_System_String_"></a>

### FinishedRun(Report, ReportRunStatus, int, string, string, string)


```csharp
public FinishedRun(Report Report, ReportRunStatus Status, int ExitCode, string ProjectRoot, string ReportPath, string ArtifactsRoot)
```


What a reporter receives once the run is over and its report is on disk (upstream's `FinishedRun`, `types.ts`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Report` | [Report](/api/E2E.Reports/Report/) | The report-1 document, as written to &lt;code class="paramref"&gt;ReportPath&lt;/code&gt;. |
| `Status` | [ReportRunStatus](/api/E2E/ReportRunStatus/) | The same status `Report.Run.Status` carries. |
| `ExitCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The exit code the run ended with (upstream's `exitCode`). The test process's own exit code is the test framework's. |
| `ProjectRoot` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Absolute project root, what the terminal shows paths relative to. |
| `ReportPath` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Where `report.json` was written. |
| `ArtifactsRoot` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Absolute directory the report's artifact paths are relative to: `<output>/results`. |

## Properties

<a id="E2E_Reports_FinishedRun_ArtifactsRoot_"></a>
<a id="E2E_Reports_FinishedRun_ArtifactsRoot"></a>

### ArtifactsRoot


```csharp
public string ArtifactsRoot { get; init; }
```


Absolute directory the report's artifact paths are relative to: `<output>/results`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_FinishedRun_ExitCode_"></a>
<a id="E2E_Reports_FinishedRun_ExitCode"></a>

### ExitCode


```csharp
public int ExitCode { get; init; }
```


The exit code the run ended with (upstream's `exitCode`). The test process's own exit code is the test framework's.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_FinishedRun_ProjectRoot_"></a>
<a id="E2E_Reports_FinishedRun_ProjectRoot"></a>

### ProjectRoot


```csharp
public string ProjectRoot { get; init; }
```


Absolute project root, what the terminal shows paths relative to.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_FinishedRun_Report_"></a>
<a id="E2E_Reports_FinishedRun_Report"></a>

### Report


```csharp
public Report Report { get; init; }
```


The report-1 document, as written to &lt;code class="paramref"&gt;ReportPath&lt;/code&gt;.

**Value:** [Report](/api/E2E.Reports/Report/)

<a id="E2E_Reports_FinishedRun_ReportPath_"></a>
<a id="E2E_Reports_FinishedRun_ReportPath"></a>

### ReportPath


```csharp
public string ReportPath { get; init; }
```


Where `report.json` was written.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_FinishedRun_Status_"></a>
<a id="E2E_Reports_FinishedRun_Status"></a>

### Status


```csharp
public ReportRunStatus Status { get; init; }
```


The same status `Report.Run.Status` carries.

**Value:** [ReportRunStatus](/api/E2E/ReportRunStatus/)
