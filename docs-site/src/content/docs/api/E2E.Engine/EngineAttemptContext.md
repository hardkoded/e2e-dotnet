---
title: "EngineAttemptContext"
description: "Upstream's EngineAttemptContext (engine/index.ts): what an engine's StartAttempt is given once per attempt, with the callbacks that carry what the engine lea..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineAttemptContext"
  hidden: true
---

<a id="E2E_Engine_EngineAttemptContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class EngineAttemptContext
```


Upstream's `EngineAttemptContext` (`engine/index.ts`): what an engine's `StartAttempt` is given once per attempt, with the
callbacks that carry what the engine learns back to the harness. Over the engine protocol each callback is a message to the client.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Properties

<a id="E2E_Engine_EngineAttemptContext_AppLog_"></a>
<a id="E2E_Engine_EngineAttemptContext_AppLog"></a>

### AppLog


```csharp
public required Action<AppLogEntry> AppLog { get; init; }
```


Takes one line of what the app did on its own. Upstream's `appLog`.

**Value:** [Action&lt;AppLogEntry&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_EngineAttemptContext_ArtifactsDir_"></a>
<a id="E2E_Engine_EngineAttemptContext_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public required string ArtifactsDir { get; init; }
```


The attempt's artifact directory. Upstream's `artifactsDir`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineAttemptContext_AttemptId_"></a>
<a id="E2E_Engine_EngineAttemptContext_AttemptId"></a>

### AttemptId


```csharp
public required string AttemptId { get; init; }
```


The attempt's id. Upstream's `attemptId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineAttemptContext_Environment_"></a>
<a id="E2E_Engine_EngineAttemptContext_Environment"></a>

### Environment


```csharp
public required Action<IReadOnlyDictionary<string, string>> Environment { get; init; }
```


Takes the facts the engine learns about what the attempt runs on. Upstream's `environment`.

**Value:** [Action&lt;IReadOnlyDictionary&lt;string, string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_EngineAttemptContext_Navigation_"></a>
<a id="E2E_Engine_EngineAttemptContext_Navigation"></a>

### Navigation


```csharp
public required Action<string> Navigation { get; init; }
```


Takes one line saying where the app went on its own. Upstream's `navigation`.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_EngineAttemptContext_ResolveSecret_"></a>
<a id="E2E_Engine_EngineAttemptContext_ResolveSecret"></a>

### ResolveSecret


```csharp
public required Func<Secret, ResolveSecretOptions?, Task<string>> ResolveSecret { get; init; }
```


The plaintext of a secret the engine holds, resolved on the client. Upstream's `resolveSecret`.

**Value:** [Func&lt;Secret, ResolveSecretOptions, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineAttemptContext_Screen_"></a>
<a id="E2E_Engine_EngineAttemptContext_Screen"></a>

### Screen


```csharp
public Action<EngineSnapshot>? Screen { get; init; }
```


Takes the screen an action left; null when the attempt keeps no trace. Upstream's `screen`.

**Value:** [Action&lt;EngineSnapshot&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_EngineAttemptContext_Signal_"></a>
<a id="E2E_Engine_EngineAttemptContext_Signal"></a>

### Signal


```csharp
public CancellationToken Signal { get; init; }
```


Aborts when the attempt is abandoned. Upstream's `signal`.

**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)
