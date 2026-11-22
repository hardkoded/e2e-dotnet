---
title: "E2ETest"
description: "Base class for an xUnit v3 test that drives the app. InitializeAsync() starts one engine session. DisposeAsync() commits the replay cache from the xUnit resu..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ETest"
  hidden: true
---

<a id="E2E_XUnit_V3_E2ETest"></a>

Namespace: [E2E.XUnit.V3](/api/E2E.XUnit.V3/)


```csharp
[TestMethodOrderer(typeof(SetupTestMethodOrderer))]
public abstract class E2ETest : E2EFixture, IAsyncLifetime, IAsyncDisposable
```


Base class for an xUnit v3 test that drives the app. [InitializeAsync()](#E2E_XUnit_V3_E2ETest_InitializeAsync) starts one
engine session. [DisposeAsync()](#E2E_XUnit_V3_E2ETest_DisposeAsync) commits the replay cache from the xUnit result: a
pass or a skip records verified acts, a failure evicts the unverified ones it recorded or
replayed, and a cancelled test leaves the cache alone. xUnit has no retry, so every test is a
first attempt and can replay. A check in a derived `DisposeAsync` after a failure verifies nothing.
Each `Expect.Soft` failure is kept on the session. The test body runs on, and the test
fails when it is disposed, with every soft failure in one message.
Settings come from [E2EFixture](/api/E2E/E2EFixture/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [E2EFixture](/api/E2E/E2EFixture/)

**Implements:** `IAsyncLifetime`, [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ETest.cs)

## Methods

<a id="E2E_XUnit_V3_E2ETest_CacheTitle_"></a>
<a id="E2E_XUnit_V3_E2ETest_CacheTitle_Xunit_Sdk_ITest_"></a>

### CacheTitle(ITest)


```csharp
protected virtual string CacheTitle(ITest test)
```


The test's title: its cache identity, and the `Test:` line every prompt's context opens with.
The default is the class and method name, with every theory
argument in full, so each test and theory row keeps its own replay. The xUnit display name
is not used, because xUnit shortens long arguments in it and a display setting can change it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `test` | `ITest` |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_XUnit_V3_E2ETest_DisposeAsync_"></a>
<a id="E2E_XUnit_V3_E2ETest_DisposeAsync"></a>

### DisposeAsync()


```csharp
public virtual ValueTask DisposeAsync()
```


Performs application-defined tasks associated with freeing, releasing, or
resetting unmanaged resources asynchronously.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) — A task that represents the asynchronous dispose operation.

<a id="E2E_XUnit_V3_E2ETest_Fixme_"></a>
<a id="E2E_XUnit_V3_E2ETest_Fixme_System_Boolean_System_String_"></a>

### Fixme(bool, string?)


```csharp
public void Fixme(bool condition = true, string? reason = null)
```


Skips the running test as fixme when &lt;code class="paramref"&gt;condition&lt;/code&gt; holds (upstream's `test.fixme(condition, reason)`). The skip reports
the cause `fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank. Like a dynamic skip
it ends the test method at once. A call from a setup test fails it with the message "Fixme() cannot skip a setup test" (the report code is `TEST_FAILED`: the host keeps only the message); a call outside a test throws
[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) where upstream has `COLLECTION_ERROR`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `condition` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_XUnit_V3_E2ETest_InitializeAsync_"></a>
<a id="E2E_XUnit_V3_E2ETest_InitializeAsync"></a>

### InitializeAsync()


```csharp
public virtual ValueTask InitializeAsync()
```


Called immediately after the class has been created, before it is used.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

<a id="E2E_XUnit_V3_E2ETest_SetTimeout_"></a>
<a id="E2E_XUnit_V3_E2ETest_SetTimeout_System_TimeSpan_"></a>

### SetTimeout(TimeSpan)


```csharp
public void SetTimeout(TimeSpan timeout)
```


Changes the running test's timeout from its body or a set-up, counted from the start of the attempt like the configured
`timeout`, which it replaces for this attempt (upstream's `test.setTimeout(timeout)`). A timeout shorter than the time already
spent times the test out at once. A timeout that is not a positive whole number of milliseconds, or above 2147483647, and a call from code
the test left running after its attempt ended are test errors, `INVALID_ARGUMENT`. A call outside a test throws
[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) where upstream has `COLLECTION_ERROR`. The port cannot tell a teardown from the body here:
a call from one is refused only once the session has completed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
