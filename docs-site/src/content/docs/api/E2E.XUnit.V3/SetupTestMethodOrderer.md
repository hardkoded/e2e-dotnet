---
title: "SetupTestMethodOrderer"
description: "Puts the setup tests (E2ESetupAttribute) of a class before its other tests, which keep xUnit's default order. .NET-only: upstream's runner orders a setup tes..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SetupTestMethodOrderer"
  hidden: true
---

<a id="E2E_XUnit_V3_SetupTestMethodOrderer"></a>

Namespace: [E2E.XUnit.V3](/api/E2E.XUnit.V3/)


```csharp
public sealed class SetupTestMethodOrderer : ITestMethodOrderer
```


Puts the setup tests ([E2ESetupAttribute](/api/E2E.XUnit.V3/E2ESetupAttribute/)) of a class before its other tests, which keep xUnit's default order. .NET-only:
upstream's runner orders a setup test before its dependents itself, and xUnit orders the test methods. [E2ETest](/api/E2E.XUnit.V3/E2ETest/) applies it;
a class with its own `[TestMethodOrderer]` replaces it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** `ITestMethodOrderer`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/SetupTestMethodOrderer.cs)

## Methods

<a id="E2E_XUnit_V3_SetupTestMethodOrderer_OrderTestMethods_"></a>
<a id="E2E_XUnit_V3_SetupTestMethodOrderer_OrderTestMethods__1_System_Collections_Generic_IReadOnlyCollection___0__"></a>

### `OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?>)`


```csharp
public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?> testMethods) where TTestMethod : ITestMethod
```


Orders test methods for execution.

**Type parameters**

| Name | Description |
| --- | --- |
| `TTestMethod` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testMethods` | [IReadOnlyCollection&lt;TTestMethod&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1) | The test methods to be ordered. |

**Returns:** [IReadOnlyCollection&lt;TTestMethod&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1) — The test methods in the order to be run.
