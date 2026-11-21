---
title: "JunitReporter"
description: "The report as JUnit XML in junit.xml beside report.json, for CI test summaries (upstream's junit() reporter, report/junit.ts). Writes nothing when the report..."
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
public sealed class JunitReporter : Reporter, IConfigurableReporter
```


The report as JUnit XML in `junit.xml` beside `report.json`, for CI test summaries (upstream's `junit()` reporter,
`report/junit.ts`). Writes nothing when the report itself was not written. Upstream's runner writes the files a reporter's
`reportFiles` returns; the port has no such hook, so this reporter writes its file itself in `OnRunFinishedAsync`,
atomically, and a failed write is one line on stderr through the reporter seam. List it in the `reporters` of `e2e.config.json`
as `E2E.Reports.JunitReporter`; the optional `options.outputFile` is upstream's `outputFile`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Reporter](/api/E2E.Reports/Reporter/)

**Implements:** [IConfigurableReporter](/api/E2E.Reports/IConfigurableReporter/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/JunitReporter.cs)

## Constructors

<a id="E2E_Reports_JunitReporter__ctor_"></a>
<a id="E2E_Reports_JunitReporter__ctor"></a>

### JunitReporter()


```csharp
public JunitReporter()
```


A reporter that writes `junit.xml` in the output directory.

<a id="E2E_Reports_JunitReporter__ctor_System_String_"></a>

### JunitReporter(string)


```csharp
public JunitReporter(string outputFile)
```


A reporter that writes the XML to &lt;code class="paramref"&gt;outputFile&lt;/code&gt;, from the project root (upstream's `outputFile`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `outputFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

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


Called once, right after the reporter is built, and only when the entry has `options`. The reporter rejects content it does not
know with a clear error that names the key, never the value: `e2e.config.json` is committed, so options hold no tokens.

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
