---
title: "E2ERunFixture"
description: "A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; TUnit has no such owner). Derive a public, no..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ERunFixture"
  hidden: true
---

<a id="E2E_TUnit_E2ERunFixture"></a>

Namespace: [E2E.TUnit](/api/E2E.TUnit/)


```csharp
public abstract class E2ERunFixture
```


A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; TUnit has no such owner). Derive a
public, non-abstract class with a public parameterless constructor from it in the test project, and the app commands that the targets declare stop
when the last test of the run is done, not when the test process exits, and the reporters run (see `E2E.Reports.Reporter`):


```csharp
public sealed class AppCommands : E2ERunFixture;
```


A project that declares no such class runs none of this: TUnit runs a hook of a library for every project that references it, so the hook
looks for the classes derived from this one in the test assembly, and does nothing when there are none. A run with several of them runs each.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.TUnit/E2ERunFixture.cs)

## Properties

<a id="E2E_TUnit_E2ERunFixture_ChangedSince_"></a>
<a id="E2E_TUnit_E2ERunFixture_ChangedSince"></a>

### ChangedSince


```csharp
protected virtual string? ChangedSince { get; }
```


The base ref to measure the change from (upstream's `--changed-since`). Unset, the `E2E_CHANGED_SINCE` environment variable; with neither, no filter runs.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_TUnit_E2ERunFixture_Config_"></a>
<a id="E2E_TUnit_E2ERunFixture_Config"></a>

### Config


```csharp
protected virtual E2EConfig Config { get; }
```


The config the filters read: the discovered `e2e.config.json`, or the defaults when there is none.

**Value:** [E2EConfig](/api/E2E/E2EConfig/)

<a id="E2E_TUnit_E2ERunFixture_Filters_"></a>
<a id="E2E_TUnit_E2ERunFixture_Filters"></a>

### Filters


```csharp
protected virtual IReadOnlyList<ITestFilter> Filters { get; }
```


The test filters, in order. They run only when [ChangedSince](#E2E_TUnit_E2ERunFixture_ChangedSince) or `E2E_CHANGED_SINCE` names a base ref; each judges
the tests and the files changed since that ref, and a test it leaves out is skipped with its reason. Empty by default.

**Value:** [IReadOnlyList&lt;ITestFilter&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_TUnit_E2ERunFixture_LastFailed_"></a>
<a id="E2E_TUnit_E2ERunFixture_LastFailed"></a>

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

<a id="E2E_TUnit_E2ERunFixture_SelectTestsAsync_"></a>
<a id="E2E_TUnit_E2ERunFixture_SelectTestsAsync_System_Threading_CancellationToken_"></a>

### SelectTestsAsync(CancellationToken)


```csharp
[Before(HookType.TestSession, "C:\\Source\\hardkoded\\e2e-dotnet-tunit\\src\\E2E.TUnit\\E2ERunFixture.cs", 49)]
public static Task SelectTestsAsync(CancellationToken cancellationToken)
```


Creates the run fixtures the test assembly declares and starts each, before the first test.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_TUnit_E2ERunFixture_StopAppCommandsAsync_"></a>
<a id="E2E_TUnit_E2ERunFixture_StopAppCommandsAsync"></a>

### StopAppCommandsAsync()


```csharp
[After(HookType.TestSession, "C:\\Source\\hardkoded\\e2e-dotnet-tunit\\src\\E2E.TUnit\\E2ERunFixture.cs", 65)]
public static Task StopAppCommandsAsync()
```


Finishes each run fixture of the run, after the last test.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
