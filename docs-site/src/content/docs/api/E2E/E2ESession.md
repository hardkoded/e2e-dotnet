---
title: "E2ESession"
description: "One test attempt: an engine session, the fixtures, and the replay cache. Call Complete after the test body. Disposing without Complete records the attempt as..."
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
Call `Complete` after the test body. Disposing without
`Complete` records the attempt as failed.

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L62)

<a id="E2E_E2ESession_App_"></a>
<a id="E2E_E2ESession_App"></a>

### App


```csharp
public App App { get; }
```


**Value:** [App](/api/E2E/App/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L58)

<a id="E2E_E2ESession_ArtifactsDir_"></a>
<a id="E2E_E2ESession_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public string ArtifactsDir { get; }
```


The absolute directory of the attempt's artifacts. Upstream's `artifactsDir`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L87)

<a id="E2E_E2ESession_AttemptId_"></a>
<a id="E2E_E2ESession_AttemptId"></a>

### AttemptId


```csharp
public string AttemptId { get; }
```


The unique id of this attempt, the one its report names. Upstream's `attemptId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L72)

<a id="E2E_E2ESession_Browser_"></a>
<a id="E2E_E2ESession_Browser"></a>

### Browser


```csharp
public Browser Browser { get; }
```


**Value:** [Browser](/api/E2E/Browser/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L60)

<a id="E2E_E2ESession_Context_"></a>
<a id="E2E_E2ESession_Context"></a>

### Context


```csharp
public TestContext Context { get; }
```


**Value:** [TestContext](/api/E2E/TestContext/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L66)

<a id="E2E_E2ESession_Fixtures_"></a>
<a id="E2E_E2ESession_Fixtures"></a>

### Fixtures


```csharp
public TestContext Fixtures { get; }
```


The session's fixtures, the same [TestContext](/api/E2E/TestContext/) as [Context](#E2E_E2ESession_Context). Upstream's `fixtures`, a `TestFixtures` that
`test.extend` can add to; the port has no `extend`, so there is no fixtures type argument.

**Value:** [TestContext](/api/E2E/TestContext/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L84)

<a id="E2E_E2ESession_HandedOff_"></a>
<a id="E2E_E2ESession_HandedOff"></a>

### HandedOff


```csharp
public int HandedOff { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L148)

<a id="E2E_E2ESession_InputTokens_"></a>
<a id="E2E_E2ESession_InputTokens"></a>

### InputTokens


```csharp
public int InputTokens { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L154)

<a id="E2E_E2ESession_Missed_"></a>
<a id="E2E_E2ESession_Missed"></a>

### Missed


```csharp
public int Missed { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L150)

<a id="E2E_E2ESession_ModelCalls_"></a>
<a id="E2E_E2ESession_ModelCalls"></a>

### ModelCalls


```csharp
public int ModelCalls { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L152)

<a id="E2E_E2ESession_OutputTokens_"></a>
<a id="E2E_E2ESession_OutputTokens"></a>

### OutputTokens


```csharp
public int OutputTokens { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L156)

<a id="E2E_E2ESession_Replayed_"></a>
<a id="E2E_E2ESession_Replayed"></a>

### Replayed


```csharp
public int Replayed { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L146)

<a id="E2E_E2ESession_RunId_"></a>
<a id="E2E_E2ESession_RunId"></a>

### RunId


```csharp
public string RunId { get; }
```


The id of the run the session belongs to: the one its report names. Upstream's `runId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L75)

<a id="E2E_E2ESession_Screen_"></a>
<a id="E2E_E2ESession_Screen"></a>

### Screen


```csharp
public Screen Screen { get; }
```


**Value:** [Screen](/api/E2E/Screen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L64)

<a id="E2E_E2ESession_Target_"></a>
<a id="E2E_E2ESession_Target"></a>

### Target


```csharp
public string Target { get; }
```


The name of the target the session runs on. Upstream's `target`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L78)

<a id="E2E_E2ESession_TracePath_"></a>
<a id="E2E_E2ESession_TracePath"></a>

### TracePath


```csharp
public string TracePath { get; }
```


The absolute path of the session's trace page. It is written when the session closes. A session of a test framework writes it if the
trace mode keeps the attempt; a hosted one ([E2EHost.OpenSessionAsync](E2EHost.md)) always does, as upstream's. Upstream's `tracePath`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L93)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L771)

<a id="E2E_E2ESession_CloseAsync_"></a>
<a id="E2E_E2ESession_CloseAsync"></a>

### CloseAsync()


```csharp
public Task<IReadOnlyList<Exception>> CloseAsync()
```


Ends the session, once, and returns what went wrong: the soft assertion failures (as one `ASSERTION_FAILED`), the steps the test
left running (`STEP_NOT_AWAITED`, whose work is cancelled), and the cleanup failures. A second call returns the same list.
Disposing a session its owner hosts throws them as an [AggregateException](https://learn.microsoft.com/dotnet/api/system.aggregateexception). Upstream's `close()`, which returns
`SerializedError[]`: the port keeps the exceptions, and [Code](/api/E2E/E2EException/#E2E_E2EException_Code) carries the code.

**Returns:** [Task&lt;IReadOnlyList&lt;Exception&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L438)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L378)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L407)

<a id="E2E_E2ESession_DisposeAsync_"></a>
<a id="E2E_E2ESession_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Performs application-defined tasks associated with freeing, releasing, or
resetting unmanaged resources asynchronously.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) — A task that represents the asynchronous dispose operation.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L483)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L144)

<a id="E2E_E2ESession_SettleAttemptAsync_"></a>
<a id="E2E_E2ESession_SettleAttemptAsync_System_Threading_CancellationToken_"></a>

### SettleAttemptAsync(CancellationToken)


```csharp
public Task SettleAttemptAsync(CancellationToken cancellationToken = default)
```


Collects a failure the engine holds from a path no step awaited (a route or dialog handler), within the cleanup budget, and
throws it as it was so its code is kept. An adapter calls it when the test body has settled, then again after the teardown, before
the verdict. Upstream's `settleAttempt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L385)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L158)

<a id="E2E_E2ESession_Steps_"></a>
<a id="E2E_E2ESession_Steps"></a>

### Steps()


```csharp
public IReadOnlyList<ReportStep> Steps()
```


The attempt's step timeline as the report tells it. Upstream's `steps()`, which returns `StepRecord[]`.

**Returns:** [IReadOnlyList&lt;ReportStep&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L96)
