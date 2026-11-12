---
title: "E2ETest"
description: "Base class for a TUnit test that drives the app. A before-test hook starts one engine session. An after-test hook commits the replay cache from the TUnit res..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ETest"
  hidden: true
---

<a id="E2E_TUnit_E2ETest"></a>

Namespace: [E2E.TUnit](/api/E2E.TUnit/)


```csharp
[TestExecutor<LooksBeforeTeardownExecutor>]
public abstract class E2ETest : E2EFixture, ITestEndEventReceiver, IEventReceiver
```


Base class for a TUnit test that drives the app. A before-test hook starts one engine session.
An after-test hook commits the replay cache from the TUnit result: a pass or a skip records
verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test
leaves the cache alone. A derived `[After(Test)]` hook that throws fails the test, and the cache sees that failure: the commit
runs when TUnit ends the test, because the result holds the hook's failure only then. The first attempt can replay. `[Retry]` attempts run live and still
record. TUnit builds one instance per test, so the session lives on the instance.
Each `Expect.Soft` failure is kept on the session. The test body runs on, and the test
fails when its after-test hook runs, with every soft failure in one message.
Settings come from [E2EFixture](/api/E2E/E2EFixture/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [E2EFixture](/api/E2E/E2EFixture/)

**Implements:** `ITestEndEventReceiver`, `IEventReceiver`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.TUnit/E2ETest.cs)

## Properties

<a id="E2E_TUnit_E2ETest_Order_"></a>
<a id="E2E_TUnit_E2ETest_Order"></a>

### Order


```csharp
public int Order { get; }
```


Runs after every hook, when the test result holds what a derived `[After(Test)]` hook threw: the verdict is the test body's, the soft
failures', or the result's own, and then the session is completed and disposed.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_TUnit_E2ETest_SessionStartToken_"></a>
<a id="E2E_TUnit_E2ETest_SessionStartToken"></a>

### SessionStartToken


```csharp
protected CancellationToken SessionStartToken { get; }
```


.NET-only: the cancellation token of the before-test hook that starts the session (TUnit's hook timeout and the run's cancellation).
[CreateEngine()](/api/E2E/E2EFixture/#E2E_E2EFixture_CreateEngine) runs inside that hook and can wait on it, so a subclass that blocks there passes this token, not the test's.

**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

<a id="E2E_TUnit_E2ETest_Stage_"></a>
<a id="E2E_TUnit_E2ETest_Stage"></a>

### Stage


```csharp
public EventReceiverStage Stage { get; }
```


Runs after every hook, when the test result holds what a derived `[After(Test)]` hook threw: the verdict is the test body's, the soft
failures', or the result's own, and then the session is completed and disposed.

**Value:** `EventReceiverStage`

## Methods

<a id="E2E_TUnit_E2ETest_CacheTitle_"></a>
<a id="E2E_TUnit_E2ETest_CacheTitle_TUnit_Core_TestContext_"></a>

### CacheTitle(TestContext)


```csharp
protected virtual string CacheTitle(TestContext test)
```


The test's title: its cache identity, and the `Test:` line every prompt's context opens with.
The default is the class and method name, with every argument in full, so each test and
each data row keeps its own replay. The TUnit display name is not used, because a display name
can be changed by an attribute or shortened for long arguments.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `test` | `TestContext` |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_TUnit_E2ETest_FinishE2ESessionAsync_"></a>
<a id="E2E_TUnit_E2ETest_FinishE2ESessionAsync_TUnit_Core_TestContext_System_Threading_CancellationToken_"></a>

### FinishE2ESessionAsync(TestContext, CancellationToken)


```csharp
[After(HookType.Test, "C:\\Source\\hardkoded\\e2e-dotnet-tunit\\src\\E2E.TUnit\\E2ETest.cs", 74)]
public Task FinishE2ESessionAsync(TestContext context, CancellationToken cancellationToken)
```


The base class hook runs after the derived ones, so a teardown has run. It collects the soft failures and a handler failure
the teardown left, and hands the session to [OnTestEnd(TestContext)](#E2E_TUnit_E2ETest_OnTestEnd_TUnit_Core_TestContext_): TUnit sets the test result, which holds a failure of a
derived `[After(Test)]` hook, only after every hook has run.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `TestContext` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_TUnit_E2ETest_OnTestEnd_"></a>
<a id="E2E_TUnit_E2ETest_OnTestEnd_TUnit_Core_TestContext_"></a>

### OnTestEnd(TestContext)


```csharp
public ValueTask OnTestEnd(TestContext context)
```


Runs after every hook, when the test result holds what a derived `[After(Test)]` hook threw: the verdict is the test body's, the soft
failures', or the result's own, and then the session is completed and disposed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `TestContext` |  |

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

<a id="E2E_TUnit_E2ETest_StartE2ESessionAsync_"></a>
<a id="E2E_TUnit_E2ETest_StartE2ESessionAsync_TUnit_Core_TestContext_System_Threading_CancellationToken_"></a>

### StartE2ESessionAsync(TestContext, CancellationToken)


```csharp
[Before(HookType.Test, "C:\\Source\\hardkoded\\e2e-dotnet-tunit\\src\\E2E.TUnit\\E2ETest.cs", 50)]
public Task StartE2ESessionAsync(TestContext context, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `TestContext` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
