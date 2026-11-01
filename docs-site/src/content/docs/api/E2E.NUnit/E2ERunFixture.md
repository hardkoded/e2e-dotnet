---
title: "E2ERunFixture"
description: "A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; NUnit has no such owner). Derive a [SetUpFixt..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ERunFixture"
  hidden: true
---

<a id="E2E_NUnit_E2ERunFixture"></a>

Namespace: [E2E.NUnit](/api/E2E.NUnit/)


```csharp
public abstract class E2ERunFixture
```


A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; NUnit has no such owner). Derive a
`[SetUpFixture]` from it, outside any namespace so it covers the whole assembly, and the app commands that the targets declare stop
when the last test of the assembly is done, not when the test process exits, and the reporters run (see `E2E.Reports.Reporter`):


```csharp
[SetUpFixture]
public sealed class AppCommands : E2ERunFixture;
```

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L18)

## Properties

<a id="E2E_NUnit_E2ERunFixture_ChangedSince_"></a>
<a id="E2E_NUnit_E2ERunFixture_ChangedSince"></a>

### ChangedSince


```csharp
protected virtual string? ChangedSince { get; }
```


The base ref to measure the change from (upstream's `--changed-since`). Unset, the `E2E_CHANGED_SINCE` environment variable; with neither, no filter runs.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L27)

<a id="E2E_NUnit_E2ERunFixture_Config_"></a>
<a id="E2E_NUnit_E2ERunFixture_Config"></a>

### Config


```csharp
protected virtual E2EConfig Config { get; }
```


The config the filters read: the discovered `e2e.config.json`, or the defaults when there is none.

**Value:** [E2EConfig](/api/E2E/E2EConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L30)

<a id="E2E_NUnit_E2ERunFixture_Filters_"></a>
<a id="E2E_NUnit_E2ERunFixture_Filters"></a>

### Filters


```csharp
protected virtual IReadOnlyList<ITestFilter> Filters { get; }
```


The test filters, in order. They run only when [ChangedSince](#E2E_NUnit_E2ERunFixture_ChangedSince) or `E2E_CHANGED_SINCE` names a base ref; each judges
the tests and the files changed since that ref, and a test it leaves out is ignored with its reason. Empty by default.

**Value:** [IReadOnlyList&lt;ITestFilter&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L24)

## Methods

<a id="E2E_NUnit_E2ERunFixture_SelectTestsAsync_"></a>
<a id="E2E_NUnit_E2ERunFixture_SelectTestsAsync"></a>

### SelectTestsAsync()


```csharp
[OneTimeSetUp]
public Task SelectTestsAsync()
```


Finds the tests of the assembly by reflection and runs the filters over them once, before the first test. The tests the filters left
out skip themselves at their start. A run with no base ref runs every test.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L36)

<a id="E2E_NUnit_E2ERunFixture_StopAppCommandsAsync_"></a>
<a id="E2E_NUnit_E2ERunFixture_StopAppCommandsAsync"></a>

### StopAppCommandsAsync()


```csharp
[OneTimeTearDown]
public Task StopAppCommandsAsync()
```


Runs the reporters that the assembly registers with `[assembly: E2EReporter(typeof(...))]` once `report.json` is final, then stops
the app commands that the tests of this process started.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L59)
