---
title: "ExploreOptions"
description: "What CancellationToken) explores, as, and with."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExploreOptions"
  hidden: true
---

<a id="E2E_ExploreOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExploreOptions
```


What `CancellationToken)` explores, as, and with.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L17)

## Properties

<a id="E2E_ExploreOptions_Agent_"></a>
<a id="E2E_ExploreOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The agent (`agents.<name>`) the exploration runs as. Defaults to `default`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L32)

<a id="E2E_ExploreOptions_Agents_"></a>
<a id="E2E_ExploreOptions_Agents"></a>

### Agents


```csharp
public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }
```


Agents given in code, by name, replacing the config's entry of the same name. Upstream's config file is code, so its agents carry
models and tools; `e2e.config.json` names a provider and a model id only, so a model of your own or a project tool is set here.

**Value:** [IReadOnlyDictionary&lt;string, AgentOptions&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L55)

<a id="E2E_ExploreOptions_AiTrace_"></a>
<a id="E2E_ExploreOptions_AiTrace"></a>

### AiTrace


```csharp
public bool AiTrace { get; init; }
```


Upstream's `--ai-trace`: records every model call to `ai-trace-<process id>.json` next to the results directory
(`.e2e` by default). `E2E_AI_TRACE=1` turns it on too.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L105)

<a id="E2E_ExploreOptions_ConfigPath_"></a>
<a id="E2E_ExploreOptions_ConfigPath"></a>

### ConfigPath


```csharp
public string? ConfigPath { get; init; }
```


The config file, instead of the nearest `e2e.config.json` above [Cwd](#E2E_ExploreOptions_Cwd).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L26)

<a id="E2E_ExploreOptions_Credentials_"></a>
<a id="E2E_ExploreOptions_Credentials"></a>

### Credentials


```csharp
public IReadOnlyDictionary<string, UserCredential>? Credentials { get; init; }
```


The accounts the explorer can sign in with, by name. Each travels into every step as the username and a password secret
named `<name>.password`, which the explorer fills with `fill_secret`; the password never reaches a model.
Upstream reads them from the config's `credentials`, which the port rejects; [User(string)](/api/E2E/Credentials/#E2E_Credentials_User_System_String_) reads one from the environment.

**Value:** [IReadOnlyDictionary&lt;string, UserCredential&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L77)

<a id="E2E_ExploreOptions_Cwd_"></a>
<a id="E2E_ExploreOptions_Cwd"></a>

### Cwd


```csharp
public string? Cwd { get; init; }
```


Where the config is looked for, and the project root when [RawConfig](#E2E_ExploreOptions_RawConfig) is set. Defaults to the working directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L23)

<a id="E2E_ExploreOptions_Debug_"></a>
<a id="E2E_ExploreOptions_Debug"></a>

### Debug


```csharp
public bool Debug { get; init; }
```


Upstream's `--debug`: prints the phase timings and the agent step table to stderr. `E2E_DEBUG=1` turns it on too.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L99)

<a id="E2E_ExploreOptions_Engine_"></a>
<a id="E2E_ExploreOptions_Engine"></a>

### Engine


```csharp
public IEngine? Engine { get; init; }
```


The engine to drive. Defaults to the engine of the target's platform: a [WebEngine](/api/E2E.Engine/WebEngine/) for `web`.

**Value:** [IEngine](/api/E2E.Engine/IEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L58)

<a id="E2E_ExploreOptions_Goal_"></a>
<a id="E2E_ExploreOptions_Goal"></a>

### Goal


```csharp
public string? Goal { get; init; }
```


What to explore for. Blank is [DefaultGoal](/api/E2E/Explore/#E2E_Explore_DefaultGoal). At most [MaxGoalChars](/api/E2E/Explore/#E2E_Explore_MaxGoalChars) characters once its secret values are redacted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L20)

<a id="E2E_ExploreOptions_Headed_"></a>
<a id="E2E_ExploreOptions_Headed"></a>

### Headed


```csharp
public bool Headed { get; init; }
```


Shows the browser window. Has no effect on an [Engine](#E2E_ExploreOptions_Engine) of your own.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L41)

<a id="E2E_ExploreOptions_MaxSteps_"></a>
<a id="E2E_ExploreOptions_MaxSteps"></a>

### MaxSteps


```csharp
public int? MaxSteps { get; init; }
```


Exploration steps, [StepBounds](/api/E2E/Explore/#E2E_Explore_StepBounds).

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L35)

<a id="E2E_ExploreOptions_Notice_"></a>
<a id="E2E_ExploreOptions_Notice"></a>

### Notice


```csharp
public Action<string>? Notice { get; init; }
```


Notices decided before the run starts: the agent chosen, a replaced executor.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L80)

<a id="E2E_ExploreOptions_OnProgress_"></a>
<a id="E2E_ExploreOptions_OnProgress"></a>

### OnProgress


```csharp
public ExploreListener? OnProgress { get; init; }
```


Receives the exploration's progress as it happens. Upstream's `onEvent` sink for the `explore` run events.

**Value:** [ExploreListener](/api/E2E/ExploreListener/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L83)

<a id="E2E_ExploreOptions_Output_"></a>
<a id="E2E_ExploreOptions_Output"></a>

### Output


```csharp
public string? Output { get; init; }
```


The results directory, over the default `.e2e/results` under the project root. Finding screenshots are kept under it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L44)

<a id="E2E_ExploreOptions_RawConfig_"></a>
<a id="E2E_ExploreOptions_RawConfig"></a>

### RawConfig


```csharp
public E2EConfig? RawConfig { get; init; }
```


A config value instead of a discovered file, for a host that builds one in code. [Cwd](#E2E_ExploreOptions_Cwd) is the project root then.

**Value:** [E2EConfig](/api/E2E/E2EConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L49)

<a id="E2E_ExploreOptions_Target_"></a>
<a id="E2E_ExploreOptions_Target"></a>

### Target


```csharp
public string? Target { get; init; }
```


The target to explore. The config declares one; a name that is not it is `UNKNOWN_TARGET`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L29)

<a id="E2E_ExploreOptions_Timeout_"></a>
<a id="E2E_ExploreOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


The run's wall clock, [TimeoutBounds](/api/E2E/Explore/#E2E_Explore_TimeoutBounds).

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L38)

<a id="E2E_ExploreOptions_Trace_"></a>
<a id="E2E_ExploreOptions_Trace"></a>

### Trace


```csharp
public RecordingMode? Trace { get; init; }
```


Which attempts keep a trace page, upstream's `--trace [mode]`; over the target's and the config's `trace`. The exploration is
one attempt, so a retry mode keeps none. Defaults to the target's mode, `retain-on-failure` unless the config says otherwise.
An exploration's own verdict, the issues its findings tell, gets no page.

**Value:** `RecordingMode?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L90)

<a id="E2E_ExploreOptions_Video_"></a>
<a id="E2E_ExploreOptions_Video"></a>

### Video


```csharp
public RecordingMode? Video { get; init; }
```


Which attempts record a video, upstream's `--video [mode]`; over the target's and the config's `video`. The exploration is
one attempt, so a retry mode records none. Defaults to the target's mode, `off` unless the config says otherwise.

**Value:** `RecordingMode?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Explore.cs#L96)
