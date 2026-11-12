---
title: "LooksBeforeTeardownExecutor"
description: "Wraps the test method, so the app is looked at the moment the body fails, as upstream does. A derived [After(Test)] hook runs after the method, and runs befo..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LooksBeforeTeardownExecutor"
  hidden: true
---

<a id="E2E_TUnit_LooksBeforeTeardownExecutor"></a>

Namespace: [E2E.TUnit](/api/E2E.TUnit/)


```csharp
public sealed class LooksBeforeTeardownExecutor : ITestExecutor
```


Wraps the test method, so the app is looked at the moment the body fails, as upstream does. A derived
`[After(Test)]` hook runs after the method, and runs before this fixture's own, so it could navigate away first.
The wrapper also keeps what the body did, because TUnit sets the test result only after its hooks have run.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** `ITestExecutor`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.TUnit/LooksBeforeTeardownExecutor.cs)

## Methods

<a id="E2E_TUnit_LooksBeforeTeardownExecutor_ExecuteTest_"></a>
<a id="E2E_TUnit_LooksBeforeTeardownExecutor_ExecuteTest_TUnit_Core_TestContext_System_Func_System_Threading_Tasks_ValueTask__"></a>

### `ExecuteTest(TestContext, Func<ValueTask>)`


```csharp
public ValueTask ExecuteTest(TestContext context, Func<ValueTask> action)
```


Executes a test within the provided test context.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `TestContext` | The test context containing information about the test being executed, including test details, dependency information, and result tracking. |
| `action` | [Func&lt;ValueTask&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) | A function that represents the test body to be executed, which returns a [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask). |

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) — A [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) that represents the asynchronous execution of the test.
When the returned task completes, the test execution has finished.

&lt;p&gt;
Called by the test framework to execute a test. The implementation
defines how the test is executed, such as on which thread or with what synchronization context.
&lt;/p&gt;
&lt;p&gt;
Custom test executors can provide specialized execution environments, such as running tests on the STA thread
or with specific threading models. This is particularly useful for testing UI components or code that has
specific threading requirements.
&lt;/p&gt;
&lt;p&gt;
The method should ensure that any exceptions thrown during test execution are properly propagated back to
the caller to ensure correct test result reporting.
&lt;/p&gt;
