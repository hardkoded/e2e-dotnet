---
title: "IEngine"
description: "The body of one target. Core knows this contract and never an engine's internals. Version is the engine version. It is not part of the replay cache key: a re..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngine"
  hidden: true
---

<a id="E2E_Engine_IEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngine
```


The body of one target. Core knows this contract and never an engine's internals.
[Version](#E2E_Engine_IEngine_Version) is the engine version. It is not part of the replay cache key:
a recording re-finds its nodes at replay.

**Derived:** [DocumentEngine](/api/E2E.Engine/DocumentEngine/), [WebEngine](/api/E2E.Engine/WebEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_IEngine_Capabilities_"></a>
<a id="E2E_Engine_IEngine_Capabilities"></a>

### Capabilities


```csharp
EngineCapabilities Capabilities { get; }
```


**Value:** [EngineCapabilities](/api/E2E.Engine/EngineCapabilities/)

<a id="E2E_Engine_IEngine_Name_"></a>
<a id="E2E_Engine_IEngine_Name"></a>

### Name


```csharp
string Name { get; }
```


The engine's name, which the report records in `run.targets[].engine` and the trace cache keys on (upstream's `engine.name`).
Defaults to [Platform](#E2E_Engine_IEngine_Platform), so an engine that does not name itself keeps compiling and keeps its cache key.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_IEngine_Platform_"></a>
<a id="E2E_Engine_IEngine_Platform"></a>

### Platform


```csharp
string Platform { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_IEngine_Secrets_"></a>
<a id="E2E_Engine_IEngine_Secrets"></a>

### Secrets


```csharp
IReadOnlyList<Secret> Secrets { get; }
```


Every secret the engine's options hold (a `Secrets.Get` handle that went into an option such as `WebBasicAuth.Password`), so the
config can check each against `secrets`. Upstream's `EngineHandle.secrets`. The default holds none, so an engine without a secret option keeps compiling.

**Value:** [IReadOnlyList&lt;Secret&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_IEngine_Version_"></a>
<a id="E2E_Engine_IEngine_Version"></a>

### Version


```csharp
string Version { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_IEngine_StartAsync_"></a>
<a id="E2E_Engine_IEngine_StartAsync_E2E_Engine_EngineStartOptions_System_Threading_CancellationToken_"></a>

### StartAsync(EngineStartOptions, CancellationToken)


```csharp
Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [EngineStartOptions](/api/E2E.Engine/EngineStartOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IEngineSession&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
