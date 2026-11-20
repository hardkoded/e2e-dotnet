---
title: "JunitReporter"
description: "The built-in junit reporter (upstream's junit(), report/junit.ts): the report as JUnit XML in junit.xml beside report.json, for CI test summaries. List it in..."
editUrl: false
prev: false
next: false
sidebar:
  label: "JunitReporter"
  hidden: true
---

<a id="E2E_Reports_JunitReporter"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public class JunitReporter : Reporter, IConfigurableReporter
```


The built-in `junit` reporter (upstream's `junit()`, `report/junit.ts`): the report as JUnit XML in `junit.xml` beside
`report.json`, for CI test summaries. List it in the `reporters` of `e2e.config.json`:
`{ "type": "E2E.Reports.JunitReporter", "options": { "outputFile": "reports/junit.xml" } }`. The port has no `reportFiles` hook, so it
writes the file itself when the run finishes (.NET-only). It writes nothing when the report itself was not written.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Reporter](/api/E2E.Reports/Reporter/)

**Implements:** [IConfigurableReporter](/api/E2E.Reports/IConfigurableReporter/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/JunitReport.cs)

## Constructors

<a id="E2E_Reports_JunitReporter__ctor_"></a>
<a id="E2E_Reports_JunitReporter__ctor"></a>

### JunitReporter()


```csharp
public JunitReporter()
```


A reporter that writes `junit.xml` in the output directory. The public constructor the config builds it with.

<a id="E2E_Reports_JunitReporter__ctor_E2E_Reports_JunitReporterOptions_"></a>

### JunitReporter(JunitReporterOptions)


```csharp
protected JunitReporter(JunitReporterOptions options)
```


A reporter with these options. For a class that derives from this one and is listed in the config.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [JunitReporterOptions](/api/E2E.Reports/JunitReporterOptions/) |  |

## Properties

<a id="E2E_Reports_JunitReporter_Name_"></a>
<a id="E2E_Reports_JunitReporter_Name"></a>

### Name


```csharp
public override string Name { get; }
```


Label in diagnostics.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Reports_JunitReporter_Configure_"></a>
<a id="E2E_Reports_JunitReporter_Configure_System_Text_Json_JsonElement_"></a>

### Configure(JsonElement)


```csharp
public void Configure(JsonElement options)
```


Reads the `options` of the config entry (.NET-only: upstream's `junit({ outputFile })` is called in code). The only option is
`outputFile`, a string. An unknown option or one that is not a string is an [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception) that names the option.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |

<a id="E2E_Reports_JunitReporter_OnRunFinishedAsync_"></a>
<a id="E2E_Reports_JunitReporter_OnRunFinishedAsync_E2E_Reports_FinishedRun_System_Threading_CancellationToken_"></a>

### OnRunFinishedAsync(FinishedRun, CancellationToken)


```csharp
public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
```


Runs once the report is written; the returned rows print under the run's output. Null prints nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `run` | [FinishedRun](/api/E2E.Reports/FinishedRun/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;ReporterSummaryRow&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
