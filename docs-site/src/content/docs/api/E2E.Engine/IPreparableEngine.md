---
title: "IPreparableEngine"
description: "An engine with upstream's optional prepare and finish hooks (declared in engine/index.ts): what a run provisions once per target before any worker starts, su..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IPreparableEngine"
  hidden: true
---

<a id="E2E_Engine_IPreparableEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IPreparableEngine : IEngine
```


An engine with upstream's optional `prepare` and `finish` hooks (declared in `engine/index.ts`): what a run provisions
once per target before any worker starts, such as a hosted device, and gives back after the last worker ended, so the provisioning
happens once per run instead of per worker. The run's own path calls them: `Runner.RunAsync` and the run fixtures of `E2E.NUnit`
and `E2E.XUnit.V3` prepare each engine once and finish it when the run ends, and a host that starts the engine on its own, as
`e2e explore` and `e2e mcp` do, calls them around the one attempt (upstream's `openStandaloneAttempt`), prepare when
the session opens and finish when it closes, on every path.
.NET-only name: upstream tests the engine handle for the members. [WebEngine](/api/E2E.Engine/WebEngine/) does not implement it, because it leases
for the attempt it starts.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IPreparableEngine.cs)

## Methods

<a id="E2E_Engine_IPreparableEngine_FinishAsync_"></a>
<a id="E2E_Engine_IPreparableEngine_FinishAsync_E2E_Engine_EngineFinishInfo_System_Threading_CancellationToken_"></a>

### FinishAsync(EngineFinishInfo, CancellationToken)


```csharp
Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
```


Runs once per run and target, after every worker of the target ended, on every exit path: a passing run, a failure, an interrupt,
or a `PrepareAsync` that threw part-way. Release what `PrepareAsync` acquired for the run here (a leased
hosted device, billed by the minute); what one worker holds belongs in its session's dispose. Runs only when `PrepareAsync`
was called, within [CleanupTimeout](/api/E2E.Engine/EngineFinishInfo/#E2E_Engine_EngineFinishInfo_CleanupTimeout), and is never cancelled by an interrupt: an interrupted run is
exactly when a leased device must still be released. &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; cancels when the budget is spent. A
failure is a cleanup error, reported with the attempt's other cleanup failures: `e2e explore` ends with it in place of the
outcome, and `close_session` shows it as a `Cleanup:` line.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EngineFinishInfo](/api/E2E.Engine/EngineFinishInfo/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IPreparableEngine_PrepareAsync_"></a>
<a id="E2E_Engine_IPreparableEngine_PrepareAsync_E2E_Engine_EnginePrepareInfo_System_Threading_CancellationToken_"></a>

### PrepareAsync(EnginePrepareInfo, CancellationToken)


```csharp
Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
```


Runs once per run and target, before any worker, app command, and the run's clock start. Download dependencies or boot devices
here, for exactly [Slots](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_Slots) worker slots, and report progress through [Log](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_Log).
No launch timeout applies: &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; cancels on an interrupt only, because a first-run download is as
long as the network makes it. A failure ends the run before any test: an [E2EException](/api/E2E/E2EException/) keeps its code, a cancel stays
a cancel (`CANCELLED` when &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; asked for it), an [OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception) is kept as it is
when &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; was not canceled, and any other exception is `ENGINE_FAILURE`.
The result can lower the target's worker cap (see [Workers](/api/E2E.Engine/EnginePrepareResult/#E2E_Engine_EnginePrepareResult_Workers)), for an engine that only learns its capacity here, and hands the workers an
environment ([Env](/api/E2E.Engine/EnginePrepareResult/#E2E_Engine_EnginePrepareResult_Env)).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EnginePrepareInfo](/api/E2E.Engine/EnginePrepareInfo/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EnginePrepareResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
