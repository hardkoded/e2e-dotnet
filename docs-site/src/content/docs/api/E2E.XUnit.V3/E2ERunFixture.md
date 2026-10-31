---
title: "E2ERunFixture"
description: "A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly fixture, and the..."
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
public sealed class E2ERunFixture : IAsyncDisposable
```


A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly
fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see Reporter):


```csharp
[assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))]
```

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ERunFixture.cs#L14)

## Methods

<a id="E2E_XUnit_V3_E2ERunFixture_DisposeAsync_"></a>
<a id="E2E_XUnit_V3_E2ERunFixture_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Runs the reporters that the test assembly registers with `[assembly: E2EReporter(typeof(...))]` once `report.json` is final, then stops the app commands that the tests of this process started.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ERunFixture.cs#L17)
