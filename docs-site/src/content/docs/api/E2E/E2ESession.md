---
title: "E2ESession"
description: "One test attempt: an engine session, the fixtures, and the replay cache. Call CancellationToken) after the test body. Disposing without CancellationToken) re..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ESession"
  hidden: true
---

<a id="E2E_E2ESession"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class E2ESession : IAsyncDisposable
```


One test attempt: an engine session, the fixtures, and the replay cache.
Call `CancellationToken)` after the test body. Disposing without
`CancellationToken)` records the attempt as failed.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L18)

## Properties

<a id="E2E_E2ESession_Agent_"></a>
<a id="E2E_E2ESession_Agent"></a>

### Agent


```csharp
public Agent Agent { get; }
```


**Value:** [Agent](/api/E2E/Agent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L55)

<a id="E2E_E2ESession_App_"></a>
<a id="E2E_E2ESession_App"></a>

### App


```csharp
public App App { get; }
```


**Value:** [App](/api/E2E/App/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L51)

<a id="E2E_E2ESession_Browser_"></a>
<a id="E2E_E2ESession_Browser"></a>

### Browser


```csharp
public Browser Browser { get; }
```


**Value:** [Browser](/api/E2E/Browser/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L53)

<a id="E2E_E2ESession_Context_"></a>
<a id="E2E_E2ESession_Context"></a>

### Context


```csharp
public TestContext Context { get; }
```


**Value:** [TestContext](/api/E2E/TestContext/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L59)

<a id="E2E_E2ESession_HandedOff_"></a>
<a id="E2E_E2ESession_HandedOff"></a>

### HandedOff


```csharp
public int HandedOff { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L117)

<a id="E2E_E2ESession_InputTokens_"></a>
<a id="E2E_E2ESession_InputTokens"></a>

### InputTokens


```csharp
public int InputTokens { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L123)

<a id="E2E_E2ESession_Missed_"></a>
<a id="E2E_E2ESession_Missed"></a>

### Missed


```csharp
public int Missed { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L119)

<a id="E2E_E2ESession_ModelCalls_"></a>
<a id="E2E_E2ESession_ModelCalls"></a>

### ModelCalls


```csharp
public int ModelCalls { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L121)

<a id="E2E_E2ESession_OutputTokens_"></a>
<a id="E2E_E2ESession_OutputTokens"></a>

### OutputTokens


```csharp
public int OutputTokens { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L125)

<a id="E2E_E2ESession_Replayed_"></a>
<a id="E2E_E2ESession_Replayed"></a>

### Replayed


```csharp
public int Replayed { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L115)

<a id="E2E_E2ESession_Screen_"></a>
<a id="E2E_E2ESession_Screen"></a>

### Screen


```csharp
public Screen Screen { get; }
```


**Value:** [Screen](/api/E2E/Screen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L57)

## Methods

<a id="E2E_E2ESession_CaptureFailureEvidenceAsync_"></a>
<a id="E2E_E2ESession_CaptureFailureEvidenceAsync_System_Exception_"></a>

### CaptureFailureEvidenceAsync(Exception)


```csharp
public Task CaptureFailureEvidenceAsync(Exception error)
```


Looks at the app once more and keeps what the screen holds, the moment a failure is known: an adapter calls it when
the test body fails, ahead of the test's own teardown, so a teardown that navigates away cannot replace the evidence
(upstream's `captureEvidence`). It looks once per attempt. A failure that first lands in the teardown is
looked at when the session is released. Only an attempt whose trace page is kept looks.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L584)

<a id="E2E_E2ESession_CloseSoftFailures_"></a>
<a id="E2E_E2ESession_CloseSoftFailures"></a>

### CloseSoftFailures()


```csharp
public TestException? CloseSoftFailures()
```


Ends `expect.soft` collection and returns one `ASSERTION_FAILED`
for every failure kept since the session started, or null. Call it when
the test body settles and fail the test with the result. Later soft
matchers throw as hard ones would.

**Returns:** [TestException](/api/E2E/TestException/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L331)

<a id="E2E_E2ESession_Complete_"></a>
<a id="E2E_E2ESession_Complete_System_Exception_System_Threading_CancellationToken_"></a>

### Complete(Exception?, CancellationToken)


```csharp
public void Complete(Exception? error = null, CancellationToken cancellationToken = default)
```


Writes verified acts and evicts the unverified ones this attempt recorded or
replayed. A passed or skipped attempt confirms only what a later check verified.
An act that missed the cache and never passed leaves its key alone. An entry a
verified replay finished, or one that already holds the same flow, is not
rewritten. A model outage leaves existing entries in place, unless a failure on the app implicated them too. A cancelled
&lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; writes nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L341)

<a id="E2E_E2ESession_DisposeAsync_"></a>
<a id="E2E_E2ESession_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Performs application-defined tasks associated with freeing, releasing, or
resetting unmanaged resources asynchronously.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) — A task that represents the asynchronous dispose operation.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L360)

<a id="E2E_E2ESession_Report_"></a>
<a id="E2E_E2ESession_Report"></a>

### Report()


```csharp
public Report? Report()
```


The attempt's report-1 document once the session is disposed and its trace page, when the mode keeps one, is written; null before that and
when the page cannot be written. It tells this attempt alone, as a single-result run: its status is the session's, not the verdict of the
test around it. Artifact paths are relative to the results directory. Upstream's `session.report()`.

**Returns:** [Report](/api/E2E.Reports/Report/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L113)

<a id="E2E_E2ESession_StartAsync_"></a>
<a id="E2E_E2ESession_StartAsync_E2E_E2ESessionOptions_System_Threading_CancellationToken_"></a>

### StartAsync(E2ESessionOptions, CancellationToken)


```csharp
public static Task<E2ESession> StartAsync(E2ESessionOptions options, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [E2ESessionOptions](/api/E2E/E2ESessionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;E2ESession&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L127)
