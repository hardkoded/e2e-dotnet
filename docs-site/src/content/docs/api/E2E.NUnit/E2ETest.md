---
title: "E2ETest"
description: "Base class for an NUnit test that drives the app. Setup starts one engine session. Teardown commits the replay cache from the NUnit result: a pass or a skip..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ETest"
  hidden: true
---

<a id="E2E_NUnit_E2ETest"></a>

Namespace: [E2E.NUnit](/api/E2E.NUnit/)


```csharp
public abstract class E2ETest : E2EFixture
```


Base class for an NUnit test that drives the app. Setup starts one engine
session. Teardown commits the replay cache from the NUnit result: a pass
or a skip records verified acts, a failure evicts the unverified ones it
recorded or replayed, and a cancelled test leaves the cache alone. The first
attempt can replay. `[Retry]` attempts run live and still record. Each
`[Repeat]` iteration is a first attempt. A check in a derived [TearDown]
after a failure verifies nothing.
Each `Expect.Soft` failure is recorded on the NUnit result, as inside
`Assert.EnterMultipleScope`, so the test fails when its body ends and
lists every soft failure. A soft failure outranks a skip: `Assert.Ignore` or
`Assert.Inconclusive` after one fails the test, and the skip reason stays in the message.
Settings come from [E2EFixture](/api/E2E/E2EFixture/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [E2EFixture](/api/E2E/E2EFixture/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_NUnit_E2ETest_CacheTitle_"></a>
<a id="E2E_NUnit_E2ETest_CacheTitle_NUnit_Framework_TestContext_TestAdapter_"></a>

### CacheTitle(TestAdapter)


```csharp
protected virtual string CacheTitle(TestContext.TestAdapter test)
```


The test's title: its cache identity, and the `Test:` line every prompt's context opens with. The default is the NUnit
full name, so each test keeps its own replay.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `test` | `TestContext.TestAdapter` |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_NUnit_E2ETest_Fail_"></a>
<a id="E2E_NUnit_E2ETest_Fail_System_Boolean_System_String_"></a>

### Fail(bool, string?)


```csharp
public void Fail(bool condition = true, string? reason = null)
```


Marks the running test expected to fail when &lt;code class="paramref"&gt;condition&lt;/code&gt; holds (upstream's `test.fail(condition, reason)`).
It does not throw: the body runs on, and the verdict follows `settleExpectedFailure`. A blank &lt;code class="paramref"&gt;reason&lt;/code&gt; becomes
`expected to fail`. A mark from a teardown, a setup test, or a serial group member is a test error, `INVALID_ARGUMENT`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `condition` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_NUnit_E2ETest_FinishE2ESerialGroupAsync_"></a>
<a id="E2E_NUnit_E2ETest_FinishE2ESerialGroupAsync"></a>

### FinishE2ESerialGroupAsync()


```csharp
[OneTimeTearDown]
public Task FinishE2ESerialGroupAsync()
```


Ends the serial group: completes the session the members shared with the first failure of the group, if any, and disposes it.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_NUnit_E2ETest_FinishE2ESessionAsync_"></a>
<a id="E2E_NUnit_E2ETest_FinishE2ESessionAsync"></a>

### FinishE2ESessionAsync()


```csharp
[TearDown]
public Task FinishE2ESessionAsync()
```


**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_NUnit_E2ETest_Fixme_"></a>
<a id="E2E_NUnit_E2ETest_Fixme_System_Boolean_System_String_"></a>

### Fixme(bool, string?)


```csharp
public void Fixme(bool condition = true, string? reason = null)
```


Skips the running test as fixme when &lt;code class="paramref"&gt;condition&lt;/code&gt; holds (upstream's `test.fixme(condition, reason)`). The skip reports
the cause `fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank. Like `Assert.Ignore`
it ends the test method at once. A call from a setup test is a test error, `INVALID_ARGUMENT`; a call outside a test throws
[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) where upstream has `COLLECTION_ERROR`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `condition` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_NUnit_E2ETest_RecordSoftFailure_"></a>
<a id="E2E_NUnit_E2ETest_RecordSoftFailure_E2E_TestException_"></a>

### RecordSoftFailure(TestException)


```csharp
protected virtual void RecordSoftFailure(TestException failure)
```


Records an `Expect.Soft` failure on the running NUnit test. The test body runs on and fails when it ends.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `failure` | [TestException](/api/E2E/TestException/) |  |

<a id="E2E_NUnit_E2ETest_SetTimeout_"></a>
<a id="E2E_NUnit_E2ETest_SetTimeout_System_TimeSpan_"></a>

### SetTimeout(TimeSpan)


```csharp
public void SetTimeout(TimeSpan timeout)
```


Changes the running test's timeout from its body or a `[SetUp]`, counted from the start of the attempt like the configured
`timeout`, which it replaces for this attempt (upstream's `test.setTimeout(timeout)`). A timeout shorter than the time already
spent times the test out at once. A timeout that is not a positive whole number of milliseconds, or above 2147483647, a call from a teardown (after the test method returned), and one
from code the test left running after it ended are test errors, `INVALID_ARGUMENT`. A call outside a test throws
[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) where upstream has `COLLECTION_ERROR`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

<a id="E2E_NUnit_E2ETest_StartE2ESessionAsync_"></a>
<a id="E2E_NUnit_E2ETest_StartE2ESessionAsync"></a>

### StartE2ESessionAsync()


```csharp
[SetUp]
public Task StartE2ESessionAsync()
```


**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
