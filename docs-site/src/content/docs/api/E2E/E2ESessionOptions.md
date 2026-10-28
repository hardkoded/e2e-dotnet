---
title: "E2ESessionOptions"
description: "How to open one E2ESession. Retries set Attempt above 1 so the cache records but does not replay, except under CacheStrict, where every attempt replays."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ESessionOptions"
  hidden: true
---

<a id="E2E_E2ESessionOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class E2ESessionOptions
```


How to open one [E2ESession](/api/E2E/E2ESession/). Retries set [Attempt](#E2E_E2ESessionOptions_Attempt) above 1 so the cache records but does not replay, except under [CacheStrict](#E2E_E2ESessionOptions_CacheStrict), where every attempt replays.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L808)

## Properties

<a id="E2E_E2ESessionOptions_ActionTimeout_"></a>
<a id="E2E_E2ESessionOptions_ActionTimeout"></a>

### ActionTimeout


```csharp
public TimeSpan ActionTimeout { get; init; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L886)

<a id="E2E_E2ESessionOptions_Agent_"></a>
<a id="E2E_E2ESessionOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The agent a call that names none uses, upstream's `agent` option of a test or run. Unset, it is `default`. An unknown name is `INVALID_ARGUMENT`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L843)

<a id="E2E_E2ESessionOptions_AgentContext_"></a>
<a id="E2E_E2ESessionOptions_AgentContext"></a>

### AgentContext


```csharp
public string? AgentContext { get; init; }
```


The default agent's upstream `context`: told to every model call, at most 16384 UTF-8 bytes.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L828)

<a id="E2E_E2ESessionOptions_AgentExecutor_"></a>
<a id="E2E_E2ESessionOptions_AgentExecutor"></a>

### AgentExecutor


```csharp
public IStepExecutor? AgentExecutor { get; init; }
```


The default agent's custom brain, upstream's `executor`. See [Executor](/api/E2E/AgentOptions/#E2E_AgentOptions_Executor).

**Value:** [IStepExecutor](/api/E2E/IStepExecutor/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L825)

<a id="E2E_E2ESessionOptions_AgentSystem_"></a>
<a id="E2E_E2ESessionOptions_AgentSystem"></a>

### AgentSystem


```csharp
public string? AgentSystem { get; init; }
```


The default agent's upstream `system`: text appended to the act rules. Judges never see it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L819)

<a id="E2E_E2ESessionOptions_AgentTools_"></a>
<a id="E2E_E2ESessionOptions_AgentTools"></a>

### AgentTools


```csharp
public IReadOnlyDictionary<string, AgentTool>? AgentTools { get; init; }
```


The default agent's project tools, offered to the model beside the agent's own. See [Tools](/api/E2E/AgentOptions/#E2E_AgentOptions_Tools).

**Value:** [IReadOnlyDictionary&lt;string, AgentTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L822)

<a id="E2E_E2ESessionOptions_Agents_"></a>
<a id="E2E_E2ESessionOptions_Agents"></a>

### Agents


```csharp
public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }
```


Named agents besides the default one, picked per call with the `Agent` option.
Each starts from the built-in defaults, not from the default agent.

**Value:** [IReadOnlyDictionary&lt;string, AgentOptions&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L840)

<a id="E2E_E2ESessionOptions_AiTrace_"></a>
<a id="E2E_E2ESessionOptions_AiTrace"></a>

### AiTrace


```csharp
public bool AiTrace { get; init; }
```


`E2E_AI_TRACE=1`, the port's counterpart of `--ai-trace`: every model call of the test is recorded in the AI SDK devtools
shape and added to `ai-trace-<process id>.json` next to the results directory (`.e2e` by default) when the session is released.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L948)

<a id="E2E_E2ESessionOptions_AssertionTimeout_"></a>
<a id="E2E_E2ESessionOptions_AssertionTimeout"></a>

### AssertionTimeout


```csharp
public TimeSpan AssertionTimeout { get; init; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L888)

<a id="E2E_E2ESessionOptions_Attempt_"></a>
<a id="E2E_E2ESessionOptions_Attempt"></a>

### Attempt


```csharp
public int Attempt { get; init; }
```


1 is the first try. Later attempts do not replay, and still record verified acts.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L954)

<a id="E2E_E2ESessionOptions_BaseUrl_"></a>
<a id="E2E_E2ESessionOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L848)

<a id="E2E_E2ESessionOptions_Cache_"></a>
<a id="E2E_E2ESessionOptions_Cache"></a>

### Cache


```csharp
public IStepCache? Cache { get; init; }
```


**Value:** [IStepCache](/api/E2E/IStepCache/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L853)

<a id="E2E_E2ESessionOptions_CacheEnabled_"></a>
<a id="E2E_E2ESessionOptions_CacheEnabled"></a>

### CacheEnabled


```csharp
public bool CacheEnabled { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L855)

<a id="E2E_E2ESessionOptions_CacheMode_"></a>
<a id="E2E_E2ESessionOptions_CacheMode"></a>

### CacheMode


```csharp
public CacheMode CacheMode { get; init; }
```


[ReadOnly](/api/E2E/CacheMode/#E2E_CacheMode_ReadOnly) replays but never writes or deletes recordings.

**Value:** [CacheMode](/api/E2E/CacheMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L858)

<a id="E2E_E2ESessionOptions_CacheStrict_"></a>
<a id="E2E_E2ESessionOptions_CacheStrict"></a>

### CacheStrict


```csharp
public bool CacheStrict { get; init; }
```


When true, a recording that no longer matches fails the act with `REPLAY_STALE`. So does a
`no-entry` miss while a [FileStepCache](/api/E2E/FileStepCache/) holds a recording of the same step under another key.
Retries replay too, and the session never writes or deletes a recording, whatever [CacheMode](#E2E_E2ESessionOptions_CacheMode) says.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L865)

<a id="E2E_E2ESessionOptions_CleanupTimeout_"></a>
<a id="E2E_E2ESessionOptions_CleanupTimeout"></a>

### CleanupTimeout


```csharp
public TimeSpan CleanupTimeout { get; init; }
```


How long the engine may take to shut down on dispose. Upstream `cleanupTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L871)

<a id="E2E_E2ESessionOptions_Debug_"></a>
<a id="E2E_E2ESessionOptions_Debug"></a>

### Debug


```csharp
public bool Debug { get; init; }
```


`E2E_DEBUG=1`, the port's counterpart of `--debug`: the session times its phases and the agent's observe, model,
and action phases, writes the table to stderr when it is released, and keeps each agent step's transcript.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L942)

<a id="E2E_E2ESessionOptions_Engine_"></a>
<a id="E2E_E2ESessionOptions_Engine"></a>

### Engine


```csharp
public required IEngine Engine { get; init; }
```


**Value:** [IEngine](/api/E2E.Engine/IEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L810)

<a id="E2E_E2ESessionOptions_Judge_"></a>
<a id="E2E_E2ESessionOptions_Judge"></a>

### Judge


```csharp
public IAgentModel? Judge { get; init; }
```


The default agent's judge for `assert`, `waitFor`, and `extract`. Defaults to [Model](#E2E_E2ESessionOptions_Model).

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L816)

<a id="E2E_E2ESessionOptions_JudgmentTimeout_"></a>
<a id="E2E_E2ESessionOptions_JudgmentTimeout"></a>

### JudgmentTimeout


```csharp
public TimeSpan JudgmentTimeout { get; init; }
```


The default agent's `judgmentTimeout`: the deadline of one `assert`, `waitFor`, or `extract`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L831)

<a id="E2E_E2ESessionOptions_LaunchTimeout_"></a>
<a id="E2E_E2ESessionOptions_LaunchTimeout"></a>

### LaunchTimeout


```csharp
public TimeSpan LaunchTimeout { get; init; }
```


How long the engine may take to start. Upstream `launchTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L868)

<a id="E2E_E2ESessionOptions_MaxModelCalls_"></a>
<a id="E2E_E2ESessionOptions_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public int MaxModelCalls { get; init; }
```


The default agent's model calls per agent call, 1 through 100.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L897)

<a id="E2E_E2ESessionOptions_MaxSteps_"></a>
<a id="E2E_E2ESessionOptions_MaxSteps"></a>

### MaxSteps


```csharp
public int MaxSteps { get; init; }
```


Actions one `ActAsync` may take, 1 through 100. [MaxSteps](/api/E2E/ActOptions/#E2E_ActOptions_MaxSteps) can only lower it.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L900)

<a id="E2E_E2ESessionOptions_Model_"></a>
<a id="E2E_E2ESessionOptions_Model"></a>

### Model


```csharp
public IAgentModel? Model { get; init; }
```


The default agent's model.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L813)

<a id="E2E_E2ESessionOptions_OnSoftFailure_"></a>
<a id="E2E_E2ESessionOptions_OnSoftFailure"></a>

### OnSoftFailure


```csharp
public Action<TestException>? OnSoftFailure { get; init; }
```


Receives each `expect.soft` failure as it happens. Null keeps them
on the session until [CloseSoftFailures()](/api/E2E/E2ESession/#E2E_E2ESession_CloseSoftFailures).

**Value:** [Action&lt;TestException&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L906)

<a id="E2E_E2ESessionOptions_ProjectRoot_"></a>
<a id="E2E_E2ESessionOptions_ProjectRoot"></a>

### ProjectRoot


```csharp
public string? ProjectRoot { get; init; }
```


Anchors the files a test names, such as a route's fulfill `Path`. Defaults to the working directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L851)

<a id="E2E_E2ESessionOptions_ProviderOptions_"></a>
<a id="E2E_E2ESessionOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The default agent's provider options, keyed by provider. See [ProviderOptions](/api/E2E/ModelRequest/#E2E_ModelRequest_ProviderOptions).

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L834)

<a id="E2E_E2ESessionOptions_ReplayTimeout_"></a>
<a id="E2E_E2ESessionOptions_ReplayTimeout"></a>

### ReplayTimeout


```csharp
public TimeSpan ReplayTimeout { get; init; }
```


How long a replay waits for each recorded target and for the recorded end state.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L894)

<a id="E2E_E2ESessionOptions_ResultsDirectory_"></a>
<a id="E2E_E2ESessionOptions_ResultsDirectory"></a>

### ResultsDirectory


```csharp
public string? ResultsDirectory { get; init; }
```


Where a failed screenshot comparison puts its diff, actual, and expected images. Defaults to `.e2e/results` under the project root.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L924)

<a id="E2E_E2ESessionOptions_StepTimeout_"></a>
<a id="E2E_E2ESessionOptions_StepTimeout"></a>

### StepTimeout


```csharp
public TimeSpan StepTimeout { get; init; }
```


How long one `ActAsync` may run unless [Timeout](/api/E2E/ActOptions/#E2E_ActOptions_Timeout) says otherwise. Judgments use [JudgmentTimeout](#E2E_E2ESessionOptions_JudgmentTimeout).

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L891)

<a id="E2E_E2ESessionOptions_TargetName_"></a>
<a id="E2E_E2ESessionOptions_TargetName"></a>

### TargetName


```csharp
public string? TargetName { get; init; }
```


The target's name in a stored screenshot's file name. Defaults to the engine's platform.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L915)

<a id="E2E_E2ESessionOptions_TestAgentContext_"></a>
<a id="E2E_E2ESessionOptions_TestAgentContext"></a>

### TestAgentContext


```csharp
public string? TestAgentContext { get; init; }
```


Upstream's `agentContext` of a test or group: appended to the context of every agent, after its own.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L846)

<a id="E2E_E2ESessionOptions_TestFailed_"></a>
<a id="E2E_E2ESessionOptions_TestFailed"></a>

### TestFailed


```csharp
public Func<bool>? TestFailed { get; init; }
```


Reports whether the test has already failed. Once it returns true, a passing check no longer
verifies acts, so a teardown assertion after a failure does not record them.

**Value:** [Func&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.func-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L912)

<a id="E2E_E2ESessionOptions_TestFile_"></a>
<a id="E2E_E2ESessionOptions_TestFile"></a>

### TestFile


```csharp
public string? TestFile { get; init; }
```


The full name of the test's class, which stands for upstream's test file: it joins the title in the result id and leads the
name of the result's directory, so two tests with one title in two classes keep apart. The NUnit and xUnit adapters set it,
and `e2e explore` sets `ExploreFile`. It also makes the session a test, as upstream's
`environment.test` does: every prompt then opens with `Test: <TestTitle>`. Null keeps the title
alone as the identity and adds no title line, as for an `e2e mcp` session, which runs no test.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L882)

<a id="E2E_E2ESessionOptions_TestTimeout_"></a>
<a id="E2E_E2ESessionOptions_TestTimeout"></a>

### TestTimeout


```csharp
public TimeSpan TestTimeout { get; init; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L884)

<a id="E2E_E2ESessionOptions_TestTitle_"></a>
<a id="E2E_E2ESessionOptions_TestTitle"></a>

### TestTitle


```csharp
public required string TestTitle { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L873)

<a id="E2E_E2ESessionOptions_Trace_"></a>
<a id="E2E_E2ESessionOptions_Trace"></a>

### Trace


```csharp
public ResolvedRecording Trace { get; init; }
```


Which attempts record a trace. The session only carries the setting; [Attempt](#E2E_E2ESessionOptions_Attempt) says which attempt this is.

**Value:** [ResolvedRecording](/api/E2E/ResolvedRecording/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L933)

<a id="E2E_E2ESessionOptions_UpdateSnapshots_"></a>
<a id="E2E_E2ESessionOptions_UpdateSnapshots"></a>

### UpdateSnapshots


```csharp
public bool UpdateSnapshots { get; init; }
```


`E2E_UPDATE_SNAPSHOTS=1`: `ToHaveScreenshotAsync` writes each stored screenshot it finds missing or
different, and passes.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L921)

<a id="E2E_E2ESessionOptions_Video_"></a>
<a id="E2E_E2ESessionOptions_Video"></a>

### Video


```csharp
public ResolvedRecording Video { get; init; }
```


Which attempts record a video. The session only carries the setting; [Attempt](#E2E_E2ESessionOptions_Attempt) says which attempt this is.

**Value:** [ResolvedRecording](/api/E2E/ResolvedRecording/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2ESession.cs#L936)
