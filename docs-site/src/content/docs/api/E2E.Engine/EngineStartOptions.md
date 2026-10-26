---
title: "EngineStartOptions"
description: "Passed to CancellationToken) once per test attempt."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineStartOptions"
  hidden: true
---

<a id="E2E_Engine_EngineStartOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class EngineStartOptions
```


Passed to `CancellationToken)` once per test attempt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L71)

## Properties

<a id="E2E_Engine_EngineStartOptions_ActionTimeout_"></a>
<a id="E2E_Engine_EngineStartOptions_ActionTimeout"></a>

### ActionTimeout


```csharp
public TimeSpan ActionTimeout { get; init; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L75)

<a id="E2E_Engine_EngineStartOptions_AppLog_"></a>
<a id="E2E_Engine_EngineStartOptions_AppLog"></a>

### AppLog


```csharp
public Action<AppLogEntry>? AppLog { get; init; }
```


Takes one line of what the app did on its own, such as console output, an exception nothing caught, or a failed request, for
the attempt's trace. Upstream's `EngineAttemptContext.appLog`. An engine may report from the moment it starts, before any
step records; the harness redacts and bounds each line, and a sink that throws never fails the attempt.

**Value:** [Action&lt;AppLogEntry&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L124)

<a id="E2E_Engine_EngineStartOptions_ArtifactsDir_"></a>
<a id="E2E_Engine_EngineStartOptions_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public string? ArtifactsDir { get; init; }
```


The attempt's artifact directory: where a recording of the attempt is written, under `video`. Upstream's
`EngineAttemptContext.artifactsDir`. An engine that records video needs it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L103)

<a id="E2E_Engine_EngineStartOptions_AttemptId_"></a>
<a id="E2E_Engine_EngineStartOptions_AttemptId"></a>

### AttemptId


```csharp
public string? AttemptId { get; init; }
```


The attempt's id, for naming what an engine leases for it at a hosted service. Defaults to a new one per start.
Upstream's `EngineAttemptContext.attemptId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L90)

<a id="E2E_Engine_EngineStartOptions_BaseUrl_"></a>
<a id="E2E_Engine_EngineStartOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L73)

<a id="E2E_Engine_EngineStartOptions_Env_"></a>
<a id="E2E_Engine_EngineStartOptions_Env"></a>

### Env


```csharp
public IReadOnlyDictionary<string, string?>? Env { get; init; }
```


The run's environment: where a hosted service's provider reads its token from, with what the engine's prepare left in it.
An engine that leases for the one attempt it starts, as [WebEngine](/api/E2E.Engine/WebEngine/) does, reads it to prepare that run; it defaults
to the process environment.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L97)

<a id="E2E_Engine_EngineStartOptions_Environment_"></a>
<a id="E2E_Engine_EngineStartOptions_Environment"></a>

### Environment


```csharp
public Action<IReadOnlyDictionary<string, string>>? Environment { get; init; }
```


Takes the facts the engine learns about what the attempt runs on, such as the browser and its user agent, for the attempt's
trace. Upstream's `EngineAttemptContext.environment`. An engine tells it at most a few times an attempt, and never
fails the attempt for it.

**Value:** [Action&lt;IReadOnlyDictionary&lt;string, string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L117)

<a id="E2E_Engine_EngineStartOptions_Navigation_"></a>
<a id="E2E_Engine_EngineStartOptions_Navigation"></a>

### Navigation


```csharp
public Action<string>? Navigation { get; init; }
```


Takes one line saying where the app went on its own, such as `navigated to /checkout` or `the app opened a new tab at /help`.
Upstream's `EngineAttemptContext.navigation`. A navigation the engine started itself is not told: its step says it.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L130)

<a id="E2E_Engine_EngineStartOptions_ProjectRoot_"></a>
<a id="E2E_Engine_EngineStartOptions_ProjectRoot"></a>

### ProjectRoot


```csharp
public string? ProjectRoot { get; init; }
```


Anchors the files the engine options name, such as an init script's `Path`. Defaults to the working directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L78)

<a id="E2E_Engine_EngineStartOptions_Screen_"></a>
<a id="E2E_Engine_EngineStartOptions_Screen"></a>

### Screen


```csharp
public Action<Observation>? Screen { get; init; }
```


Takes the screen an action left, for the attempt's trace. Upstream's `EngineAttemptContext.screen`. Null when the
attempt keeps no trace, and an engine then takes no screen. An engine may read the screen on the action's path; it never
fails the action for it, and a sink that throws never fails the attempt.

**Value:** [Action&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L110)
