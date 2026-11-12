---
title: "E2ERunFixture"
description: "A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ERunFixture"
  hidden: true
---

<a id="E2E_XUnit_V3_E2ERunFixture"></a>

Namespace: [E2E.XUnit.V3](/api/E2E.XUnit.V3/)


```csharp
public class E2ERunFixture : IAsyncLifetime, IAsyncDisposable
```


A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly
fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see `E2E.Reports.Reporter`):


```csharp
[assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))]
```


To give the run test filters, derive a fixture, override [Filters](#E2E_XUnit_V3_E2ERunFixture_Filters), and register that type instead.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** `IAsyncLifetime`, [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ERunFixture.cs)

## Properties

<a id="E2E_XUnit_V3_E2ERunFixture_ChangedSince_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_ChangedSince"></a>

### ChangedSince


```csharp
protected virtual string? ChangedSince { get; }
```


The base ref to measure the change from (upstream's `--changed-since`). Unset, the `E2E_CHANGED_SINCE` environment variable; with neither, no filter runs.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_XUnit_V3_E2ERunFixture_Config_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_Config"></a>

### Config


```csharp
protected virtual E2EConfig Config { get; }
```


The config the filters read: the discovered `e2e.config.json`, or the defaults when there is none.

**Value:** [E2EConfig](/api/E2E/E2EConfig/)

<a id="E2E_XUnit_V3_E2ERunFixture_Filters_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_Filters"></a>

### Filters


```csharp
protected virtual IReadOnlyList<ITestFilter> Filters { get; }
```


The test filters, in order. They run only when [ChangedSince](#E2E_XUnit_V3_E2ERunFixture_ChangedSince) or `E2E_CHANGED_SINCE` names a base ref; each judges
the tests and the files changed since that ref, and a test it leaves out is skipped with its reason. Empty by default.

**Value:** [IReadOnlyList&lt;ITestFilter&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_XUnit_V3_E2ERunFixture_LastFailed_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_LastFailed"></a>

### LastFailed


```csharp
protected virtual bool LastFailed { get; }
```


Whether to run only the tests the previous run did not pass (upstream's `--last-failed`). Unset, the `E2E_LAST_FAILED` environment variable
(`1` or `true`). The previous run's `report.json` is read from the output directory before this run replaces it; none there is
`NO_LAST_RUN`. The tests it passed skip themselves with the reason `did not fail in the last run`, the filters do not run, and
[LastRun](/api/E2E.Reports/FinishedRun/#E2E_Reports_FinishedRun_LastRun) hands the previous report to the reporters.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_XUnit_V3_E2ERunFixture_DisposeAsync_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Finishes the engines the run prepared, ends the run's events and runs the reporters that [Config](#E2E_XUnit_V3_E2ERunFixture_Config) lists in `reporters` once `report.json` is final, then
stops the app commands that the tests of this process started, and deletes the sessions that setup tests saved (see `E2ESetupAttribute`).

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

<a id="E2E_XUnit_V3_E2ERunFixture_InitializeAsync_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_InitializeAsync"></a>

### InitializeAsync()


```csharp
public ValueTask InitializeAsync()
```


Builds the reporters of [Config](#E2E_XUnit_V3_E2ERunFixture_Config) and starts the run's events, then finds the tests of the assembly by reflection and runs the filters over them
once, before the first test. The tests the filters left out skip themselves at their start. A run with no base ref runs every test.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
