---
title: "E2ERunFixture"
description: "A .NET-only run-level hook (upstream's runner stops the app commands when the run ends; NUnit has no such owner). Derive a [SetUpFixture] from it, outside an..."
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


A .NET-only run-level hook (upstream's runner stops the app commands when the run ends; NUnit has no such owner). Derive a
`[SetUpFixture]` from it, outside any namespace so it covers the whole assembly, and the app commands that the targets declare stop
when the last test of the assembly is done, not when the test process exits:


```csharp
[SetUpFixture]
public sealed class AppCommands : E2ERunFixture;
```

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L18)

## Methods

<a id="E2E_NUnit_E2ERunFixture_StopAppCommandsAsync_"></a>
<a id="E2E_NUnit_E2ERunFixture_StopAppCommandsAsync"></a>

### StopAppCommandsAsync()


```csharp
[OneTimeTearDown]
public Task StopAppCommandsAsync()
```


Stops the app commands that the tests of this process started.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ERunFixture.cs#L21)
