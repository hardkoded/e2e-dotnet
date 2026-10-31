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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ETest.cs#L27)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ETest.cs#L34)

<a id="E2E_NUnit_E2ETest_FinishE2ESessionAsync_"></a>
<a id="E2E_NUnit_E2ETest_FinishE2ESessionAsync"></a>

### FinishE2ESessionAsync()


```csharp
[TearDown]
public Task FinishE2ESessionAsync()
```


**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ETest.cs#L55)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ETest.cs#L41)

<a id="E2E_NUnit_E2ETest_StartE2ESessionAsync_"></a>
<a id="E2E_NUnit_E2ETest_StartE2ESessionAsync"></a>

### StartE2ESessionAsync()


```csharp
[SetUp]
public Task StartE2ESessionAsync()
```


**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ETest.cs#L47)
