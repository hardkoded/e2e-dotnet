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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ETest.cs#L22)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ETest.cs#L34)

<a id="E2E_XUnit_V3_E2ETest_DisposeAsync_"></a>
<a id="E2E_XUnit_V3_E2ETest_DisposeAsync"></a>

### DisposeAsync()


```csharp
public virtual ValueTask DisposeAsync()
```


Performs application-defined tasks associated with freeing, releasing, or
resetting unmanaged resources asynchronously.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) — A task that represents the asynchronous dispose operation.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ETest.cs#L131)

<a id="E2E_XUnit_V3_E2ETest_InitializeAsync_"></a>
<a id="E2E_XUnit_V3_E2ETest_InitializeAsync"></a>

### InitializeAsync()


```csharp
public virtual ValueTask InitializeAsync()
```


Called immediately after the class has been created, before it is used.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ETest.cs#L67)
