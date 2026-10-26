---
title: "E2EFixture"
description: "The part of a test base class that every test framework shares. It holds the settings and starts one E2ESession per test. A framework adapter, such as E2E.NU..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2EFixture"
  hidden: true
---

<a id="E2E_E2EFixture"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public abstract class E2EFixture
```


The part of a test base class that every test framework shares. It holds the settings and
starts one [E2ESession](/api/E2E/E2ESession/) per test. A framework adapter, such as `E2E.NUnit.E2ETest`,
calls `MethodBase)` before the test body and commits the session from the
framework's result after it.

&lt;p&gt;
Settings come from [Config](#E2E_E2EFixture_Config), the nearest `e2e.config.json` above the
test assembly or the working directory. Override a property to change one value for a fixture.
&lt;/p&gt;

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Derived:** [E2ETest](/api/E2E.NUnit/E2ETest/), [E2ETest](/api/E2E.XUnit.V3/E2ETest/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L19)

## Properties

<a id="E2E_E2EFixture_ActionTimeout_"></a>
<a id="E2E_E2EFixture_ActionTimeout"></a>

### ActionTimeout


```csharp
protected virtual TimeSpan ActionTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L153)

<a id="E2E_E2EFixture_Agent_"></a>
<a id="E2E_E2EFixture_Agent"></a>

### Agent


```csharp
protected Agent Agent { get; }
```


**Value:** [Agent](/api/E2E/Agent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L29)

<a id="E2E_E2EFixture_AgentContext_"></a>
<a id="E2E_E2EFixture_AgentContext"></a>

### AgentContext


```csharp
protected virtual string? AgentContext { get; }
```


`agents.default.context`: project context told to every model call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L74)

<a id="E2E_E2EFixture_AgentExecutor_"></a>
<a id="E2E_E2EFixture_AgentExecutor"></a>

### AgentExecutor


```csharp
protected virtual IStepExecutor? AgentExecutor { get; }
```


The custom brain of `agents.default`, upstream's `executor`. None by default: an executor is code, so the config file cannot name one.
With one, [AgentSystem](#E2E_E2EFixture_AgentSystem) and [AgentTools](#E2E_E2EFixture_AgentTools) must be unset.

**Value:** [IStepExecutor](/api/E2E/IStepExecutor/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L71)

<a id="E2E_E2EFixture_AgentSystem_"></a>
<a id="E2E_E2EFixture_AgentSystem"></a>

### AgentSystem


```csharp
protected virtual string? AgentSystem { get; }
```


`agents.default.system`: text appended to the act rules.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L62)

<a id="E2E_E2EFixture_AgentTools_"></a>
<a id="E2E_E2EFixture_AgentTools"></a>

### AgentTools


```csharp
protected virtual IReadOnlyDictionary<string, AgentTool>? AgentTools { get; }
```


The project tools of `agents.default`, offered to the model beside the agent's own. None by default; a device test passes `MobileTools.Create(engine)`.

**Value:** [IReadOnlyDictionary&lt;string, AgentTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L65)

<a id="E2E_E2EFixture_AiTrace_"></a>
<a id="E2E_E2EFixture_AiTrace"></a>

### AiTrace


```csharp
protected virtual bool AiTrace { get; }
```


`E2E_AI_TRACE`: when set to `1` or `true`, the port's counterpart of `--ai-trace`. Each test records every model call
in the AI SDK devtools shape. The process writes them to `ai-trace-<process id>.json` next to the results directory
(`.e2e` by default); read it with `npx unbox-ai`. Override to turn it on in code.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L144)

<a id="E2E_E2EFixture_App_"></a>
<a id="E2E_E2EFixture_App"></a>

### App


```csharp
protected App App { get; }
```


**Value:** [App](/api/E2E/App/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L25)

<a id="E2E_E2EFixture_AssertionTimeout_"></a>
<a id="E2E_E2EFixture_AssertionTimeout"></a>

### AssertionTimeout


```csharp
protected virtual TimeSpan AssertionTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L155)

<a id="E2E_E2EFixture_BaseUrl_"></a>
<a id="E2E_E2EFixture_BaseUrl"></a>

### BaseUrl


```csharp
protected virtual string? BaseUrl { get; }
```


`targets[0].app.url`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L95)

<a id="E2E_E2EFixture_Browser_"></a>
<a id="E2E_E2EFixture_Browser"></a>

### Browser


```csharp
protected Browser Browser { get; }
```


**Value:** [Browser](/api/E2E/Browser/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L27)

<a id="E2E_E2EFixture_CacheDirectory_"></a>
<a id="E2E_E2EFixture_CacheDirectory"></a>

### CacheDirectory


```csharp
protected virtual string CacheDirectory { get; }
```


`cache.dir`, resolved against the config directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L101)

<a id="E2E_E2EFixture_CacheMode_"></a>
<a id="E2E_E2EFixture_CacheMode"></a>

### CacheMode


```csharp
protected virtual CacheMode CacheMode { get; }
```


`cache.mode`. Unset, it is read-write locally and read-only in CI.

**Value:** [CacheMode](/api/E2E/CacheMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L104)

<a id="E2E_E2EFixture_CacheStrict_"></a>
<a id="E2E_E2EFixture_CacheStrict"></a>

### CacheStrict


```csharp
protected virtual bool CacheStrict { get; }
```


`cache.strict`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L107)

<a id="E2E_E2EFixture_CleanupTimeout_"></a>
<a id="E2E_E2EFixture_CleanupTimeout"></a>

### CleanupTimeout


```csharp
protected virtual TimeSpan CleanupTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L157)

<a id="E2E_E2EFixture_Config_"></a>
<a id="E2E_E2EFixture_Config"></a>

### Config


```csharp
protected virtual E2EConfig Config { get; }
```


The discovered `e2e.config.json`, or the defaults when there is none. Loaded once per test run.

**Value:** [E2EConfig](/api/E2E/E2EConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L36)

<a id="E2E_E2EFixture_Context_"></a>
<a id="E2E_E2EFixture_Context"></a>

### Context


```csharp
protected TestContext Context { get; }
```


**Value:** [TestContext](/api/E2E/TestContext/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L33)

<a id="E2E_E2EFixture_Debug_"></a>
<a id="E2E_E2EFixture_Debug"></a>

### Debug


```csharp
protected virtual bool Debug { get; }
```


`E2E_DEBUG`: when set to `1` or `true`, the port's counterpart of `--debug`. Each test prints its phase
timings to stderr and keeps the transcript of every agent step. Override to turn it on in code.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L137)

<a id="E2E_E2EFixture_JudgmentTimeout_"></a>
<a id="E2E_E2EFixture_JudgmentTimeout"></a>

### JudgmentTimeout


```csharp
protected virtual TimeSpan JudgmentTimeout { get; }
```


`agents.default.judgmentTimeout`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L83)

<a id="E2E_E2EFixture_LaunchTimeout_"></a>
<a id="E2E_E2EFixture_LaunchTimeout"></a>

### LaunchTimeout


```csharp
protected virtual TimeSpan LaunchTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L151)

<a id="E2E_E2EFixture_MaxInputTokens_"></a>
<a id="E2E_E2EFixture_MaxInputTokens"></a>

### MaxInputTokens


```csharp
protected virtual int MaxInputTokens { get; }
```


`agents.default.maxInputTokens`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L89)

<a id="E2E_E2EFixture_MaxModelCalls_"></a>
<a id="E2E_E2EFixture_MaxModelCalls"></a>

### MaxModelCalls


```csharp
protected virtual int MaxModelCalls { get; }
```


`agents.default.maxModelCalls`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L166)

<a id="E2E_E2EFixture_MaxObservationBytes_"></a>
<a id="E2E_E2EFixture_MaxObservationBytes"></a>

### MaxObservationBytes


```csharp
protected virtual int MaxObservationBytes { get; }
```


`agents.default.maxObservationBytes`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L86)

<a id="E2E_E2EFixture_MaxSteps_"></a>
<a id="E2E_E2EFixture_MaxSteps"></a>

### MaxSteps


```csharp
protected virtual int MaxSteps { get; }
```


`agents.default.maxSteps`: actions one `ActAsync` may take. `ActOptions.MaxSteps` can only lower it.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L169)

<a id="E2E_E2EFixture_ProjectRoot_"></a>
<a id="E2E_E2EFixture_ProjectRoot"></a>

### ProjectRoot


```csharp
protected virtual string ProjectRoot { get; }
```


Anchors the files a test names, such as a route's fulfill path. Defaults to the config's directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L98)

<a id="E2E_E2EFixture_ProviderOptions_"></a>
<a id="E2E_E2EFixture_ProviderOptions"></a>

### ProviderOptions


```csharp
protected virtual IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; }
```


`agents.default.providerOptions`.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L92)

<a id="E2E_E2EFixture_ReplayTimeout_"></a>
<a id="E2E_E2EFixture_ReplayTimeout"></a>

### ReplayTimeout


```csharp
protected virtual TimeSpan ReplayTimeout { get; }
```


How long a replay waits for each recorded target and for the recorded end state.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L163)

<a id="E2E_E2EFixture_ResultsDirectory_"></a>
<a id="E2E_E2EFixture_ResultsDirectory"></a>

### ResultsDirectory


```csharp
protected virtual string? ResultsDirectory { get; }
```


Where a failed screenshot comparison puts its diff, actual, and expected images. Defaults to `.e2e/results` under [ProjectRoot](#E2E_E2EFixture_ProjectRoot).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L116)

<a id="E2E_E2EFixture_RunAgent_"></a>
<a id="E2E_E2EFixture_RunAgent"></a>

### RunAgent


```csharp
protected virtual string? RunAgent { get; }
```


`E2E_AGENT`: the agent that calls use when neither the call nor the test ([AgentAttribute](/api/E2E/AgentAttribute/)) names one,
the port's counterpart of `--agent`. Unset, it is `default`. Override to set it in code.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L80)

<a id="E2E_E2EFixture_Screen_"></a>
<a id="E2E_E2EFixture_Screen"></a>

### Screen


```csharp
protected Screen Screen { get; }
```


**Value:** [Screen](/api/E2E/Screen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L31)

<a id="E2E_E2EFixture_Secrets_"></a>
<a id="E2E_E2EFixture_Secrets"></a>

### Secrets


```csharp
protected Secrets Secrets { get; }
```


Secrets declared in the config, with `E2E_SECRET_{NAME}` overrides.

**Value:** [Secrets](/api/E2E/Secrets/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L39)

<a id="E2E_E2EFixture_StepTimeout_"></a>
<a id="E2E_E2EFixture_StepTimeout"></a>

### StepTimeout


```csharp
protected virtual TimeSpan StepTimeout { get; }
```


How long one `ActAsync` may run unless its options say otherwise. A .NET-only setting.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L160)

<a id="E2E_E2EFixture_TargetName_"></a>
<a id="E2E_E2EFixture_TargetName"></a>

### TargetName


```csharp
protected virtual string TargetName { get; }
```


`targets[0].name`: the target part of a stored screenshot's file name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L119)

<a id="E2E_E2EFixture_TestTimeout_"></a>
<a id="E2E_E2EFixture_TestTimeout"></a>

### TestTimeout


```csharp
protected virtual TimeSpan TestTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L149)

<a id="E2E_E2EFixture_Trace_"></a>
<a id="E2E_E2EFixture_Trace"></a>

### Trace


```csharp
protected virtual ResolvedRecording Trace { get; }
```


`trace` for the target: `E2E_TRACE`, else `targets[0].trace`, else the config's `trace`, else
`retain-on-failure`. Override to set it for one fixture.

**Value:** [ResolvedRecording](/api/E2E/ResolvedRecording/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L125)

<a id="E2E_E2EFixture_UpdateSnapshots_"></a>
<a id="E2E_E2EFixture_UpdateSnapshots"></a>

### UpdateSnapshots


```csharp
protected virtual bool UpdateSnapshots { get; }
```


`E2E_UPDATE_SNAPSHOTS`: when set to `1`, `ToHaveScreenshotAsync` writes each stored screenshot it
finds missing or different, and passes. Override to turn it on in code.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L113)

<a id="E2E_E2EFixture_Video_"></a>
<a id="E2E_E2EFixture_Video"></a>

### Video


```csharp
protected virtual ResolvedRecording Video { get; }
```


`video` for the target: `E2E_VIDEO`, else `targets[0].video`, else the config's `video`, else
`off`. Override to set it for one fixture.

**Value:** [ResolvedRecording](/api/E2E/ResolvedRecording/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L131)

## Methods

<a id="E2E_E2EFixture_CaptureFailureEvidenceAsync_"></a>
<a id="E2E_E2EFixture_CaptureFailureEvidenceAsync_System_Exception_"></a>

### CaptureFailureEvidenceAsync(Exception)


```csharp
protected Task CaptureFailureEvidenceAsync(Exception error)
```


Looks at the app now, for an adapter to call when the test body has failed and before the test's own teardown runs,
so a teardown that navigates away cannot replace the evidence. See [CaptureFailureEvidenceAsync(Exception)](/api/E2E/E2ESession/#E2E_E2ESession_CaptureFailureEvidenceAsync_System_Exception_).
Does nothing when setup never started a session.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L251)

<a id="E2E_E2EFixture_CreateAgents_"></a>
<a id="E2E_E2EFixture_CreateAgents"></a>

### CreateAgents()


```csharp
protected virtual IReadOnlyDictionary<string, AgentOptions> CreateAgents()
```


The named agents besides `default`, picked per call with the `Agent` option.
Defaults to every other `agents.<name>` entry in the config.

**Returns:** [IReadOnlyDictionary&lt;string, AgentOptions&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L54)

<a id="E2E_E2EFixture_CreateEngine_"></a>
<a id="E2E_E2EFixture_CreateEngine"></a>

### CreateEngine()


```csharp
protected virtual IEngine CreateEngine()
```


Browser engine. Override to supply a [DocumentEngine](/api/E2E.Engine/DocumentEngine/) or a custom engine.

**Returns:** [IEngine](/api/E2E.Engine/IEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L42)

<a id="E2E_E2EFixture_CreateJudge_"></a>
<a id="E2E_E2EFixture_CreateJudge"></a>

### CreateJudge()


```csharp
protected virtual IAgentModel? CreateJudge()
```


The judge for `agents.default`: `agents.default.judge`, or null to judge with [CreateModel()](#E2E_E2EFixture_CreateModel).

**Returns:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L48)

<a id="E2E_E2EFixture_CreateModel_"></a>
<a id="E2E_E2EFixture_CreateModel"></a>

### CreateModel()


```csharp
protected virtual IAgentModel? CreateModel()
```


The model for `agents.default`, or null when the config sets no model.

**Returns:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L45)

<a id="E2E_E2EFixture_StartSessionAsync_"></a>
<a id="E2E_E2EFixture_StartSessionAsync_System_String_System_Int32_System_Action_E2E_TestException__System_Func_System_Boolean__System_Threading_CancellationToken_System_Reflection_MethodBase_"></a>

### `StartSessionAsync(string, int, Action<TestException>?, Func<bool>, CancellationToken, MethodBase?)`


```csharp
protected Task StartSessionAsync(string title, int attempt, Action<TestException>? onSoftFailure, Func<bool> testFailed, CancellationToken cancellationToken, MethodBase? testMethod = null)
```


Starts the session for one test. &lt;code class="paramref"&gt;title&lt;/code&gt; is the cache identity,
&lt;code class="paramref"&gt;attempt&lt;/code&gt; is 1 for a first attempt, &lt;code class="paramref"&gt;onSoftFailure&lt;/code&gt; reports
an `Expect.Soft` failure to the framework (null keeps them on the session), and
&lt;code class="paramref"&gt;testFailed&lt;/code&gt; says whether the framework already failed the test.
&lt;code class="paramref"&gt;testMethod&lt;/code&gt; is the test's method, which carries its [AgentAttribute](/api/E2E/AgentAttribute/) and [AgentContextAttribute](/api/E2E/AgentContextAttribute/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `title` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `attempt` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `onSoftFailure` | [Action&lt;TestException&gt;](https://learn.microsoft.com/dotnet/api/system.action-1) |  |
| `testFailed` | [Func&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `testMethod` | [MethodBase](https://learn.microsoft.com/dotnet/api/system.reflection.methodbase) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L180)

<a id="E2E_E2EFixture_TakeSession_"></a>
<a id="E2E_E2EFixture_TakeSession"></a>

### TakeSession()


```csharp
protected E2ESession? TakeSession()
```


Hands the running session to the adapter to complete and dispose, or null when setup never started one.

**Returns:** [E2ESession](/api/E2E/E2ESession/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/E2EFixture.cs#L254)
