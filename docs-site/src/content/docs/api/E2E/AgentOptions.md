---
title: "AgentOptions"
description: "One named agent, as upstream agents.<name>. Each entry starts from the built-in defaults; no agent inherits another's values. A call picks one with its Agent..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentOptions"
  hidden: true
---

<a id="E2E_AgentOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AgentOptions
```


One named agent, as upstream `agents.<name>`. Each entry starts from the built-in
defaults; no agent inherits another's values. A call picks one with its `Agent` option.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L15)

## Properties

<a id="E2E_AgentOptions_Context_"></a>
<a id="E2E_AgentOptions_Context"></a>

### Context


```csharp
public string? Context { get; init; }
```


What the app calls things, told to every model call this agent makes, act turns and judgments
alike. At most 16384 UTF-8 bytes.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L46)

<a id="E2E_AgentOptions_Executor_"></a>
<a id="E2E_AgentOptions_Executor"></a>

### Executor


```csharp
public IStepExecutor? Executor { get; init; }
```


The custom brain `act` and `assert` dispatch to, upstream's `executor`. Without one, the built-in agent
acts. An executor replaces the built-in agent's prompt and tools, so it cannot be combined with [System](#E2E_AgentOptions_System) or
[Tools](#E2E_AgentOptions_Tools). It cannot be written in `e2e.config.json`: an executor is code.

**Value:** [IStepExecutor](/api/E2E/IStepExecutor/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L34)

<a id="E2E_AgentOptions_Judge_"></a>
<a id="E2E_AgentOptions_Judge"></a>

### Judge


```csharp
public IAgentModel? Judge { get; init; }
```


The model that judges `assert`, `waitFor`, and `extract`. Defaults to
[Model](#E2E_AgentOptions_Model). A judge of its own separates the model that grades a flow from the one that drove it.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L27)

<a id="E2E_AgentOptions_JudgmentTimeout_"></a>
<a id="E2E_AgentOptions_JudgmentTimeout"></a>

### JudgmentTimeout


```csharp
public TimeSpan JudgmentTimeout { get; init; }
```


Deadline of one `assert`, `waitFor`, or `extract`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L55)

<a id="E2E_AgentOptions_MaxInputTokens_"></a>
<a id="E2E_AgentOptions_MaxInputTokens"></a>

### MaxInputTokens


```csharp
public int MaxInputTokens { get; init; }
```


The input one model request of this agent may carry, upstream's `maxInputTokens`, 1 through 1000000, counted as upstream's
`tokenUpperBound` does: in UTF-8 bytes. The screen is cut to fit under it, and a judgment whose request is over it fails
with `STEP_BUDGET_EXHAUSTED` before it is sent.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L68)

<a id="E2E_AgentOptions_MaxModelCalls_"></a>
<a id="E2E_AgentOptions_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public int? MaxModelCalls { get; init; }
```


Model requests per agent call, 1 through 100. Null leaves it unset: [MaxModelCalls](/api/E2E/E2EDefaults/#E2E_E2EDefaults_MaxModelCalls) applies, and `Explore` sets its own.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L52)

<a id="E2E_AgentOptions_MaxObservationBytes_"></a>
<a id="E2E_AgentOptions_MaxObservationBytes"></a>

### MaxObservationBytes


```csharp
public int MaxObservationBytes { get; init; }
```


The most bytes of the screen one model request carries, upstream's `maxObservationBytes`, 1024 through 16777216. A
screen over it is cut and ends with a marker. The per-call [MaxInputTokens](#E2E_AgentOptions_MaxInputTokens) ceiling binds first on a large screen.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L61)

<a id="E2E_AgentOptions_MaxSteps_"></a>
<a id="E2E_AgentOptions_MaxSteps"></a>

### MaxSteps


```csharp
public int? MaxSteps { get; init; }
```


Committed actions per `act`, 1 through 100. Null leaves it unset: [MaxSteps](/api/E2E/E2EDefaults/#E2E_E2EDefaults_MaxSteps) applies, and `Explore` sets its own.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L49)

<a id="E2E_AgentOptions_Model_"></a>
<a id="E2E_AgentOptions_Model"></a>

### Model


```csharp
public IAgentModel? Model { get; init; }
```


The model behind `act`. There is no implicit default. A custom [Executor](#E2E_AgentOptions_Executor) that brought a model of its
own supplies it; naming a different one here is `INVALID_CONFIG`.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L21)

<a id="E2E_AgentOptions_ProviderOptions_"></a>
<a id="E2E_AgentOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Provider options every model call carries, keyed by provider. [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)
adds the fields under its [Provider](/api/E2E/OpenAiCompatibleModelOptions/#E2E_OpenAiCompatibleModelOptions_Provider) key to the request body as given.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L74)

<a id="E2E_AgentOptions_System_"></a>
<a id="E2E_AgentOptions_System"></a>

### System


```csharp
public string? System { get; init; }
```


How the acting agent should work, appended to the built-in execution rules. Only the act loop
reads it; the judges never see it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L40)

<a id="E2E_AgentOptions_Tools_"></a>
<a id="E2E_AgentOptions_Tools"></a>

### Tools


```csharp
public IReadOnlyDictionary<string, AgentTool>? Tools { get; init; }
```


Project tools offered to the model beside the agent's own, by name. A tool is offered only on the platforms it
declares, and a name the agent already uses is refused.

**Value:** [IReadOnlyDictionary&lt;string, AgentTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentOptions.cs#L80)
