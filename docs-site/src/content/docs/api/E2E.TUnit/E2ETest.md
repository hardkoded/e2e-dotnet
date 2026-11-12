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
public abstract class E2ETest : E2EFixture
```


Base class for a TUnit test that drives the app. A before-test hook starts one engine session.
An after-test hook commits the replay cache from the TUnit result: a pass or a skip records
verified acts, a failure evicts the unverified ones it recorded or replayed, and a cancelled test
leaves the cache alone. The first attempt can replay. `[Retry]` attempts run live and still
record. TUnit builds one instance per test, so the session lives on the instance.
Each `Expect.Soft` failure is kept on the session. The test body runs on, and the test
fails when its after-test hook runs, with every soft failure in one message.
Settings come from [E2EFixture](/api/E2E/E2EFixture/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [E2EFixture](/api/E2E/E2EFixture/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.TUnit/E2ETest.cs)

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
[After(HookType.Test, "C:\\Source\\hardkoded\\e2e-dotnet-tunit\\src\\E2E.TUnit\\E2ETest.cs", 55)]
public Task FinishE2ESessionAsync(TestContext context, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `TestContext` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_TUnit_E2ETest_StartE2ESessionAsync_"></a>
<a id="E2E_TUnit_E2ETest_StartE2ESessionAsync_TUnit_Core_TestContext_System_Threading_CancellationToken_"></a>

### StartE2ESessionAsync(TestContext, CancellationToken)


```csharp
[Before(HookType.Test, "C:\\Source\\hardkoded\\e2e-dotnet-tunit\\src\\E2E.TUnit\\E2ETest.cs", 41)]
public Task StartE2ESessionAsync(TestContext context, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `TestContext` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
