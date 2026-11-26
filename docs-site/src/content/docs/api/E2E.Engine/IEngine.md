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

<a id="E2E_Engine_IEngine_FileApps_"></a>
<a id="E2E_Engine_IEngine_FileApps"></a>

### FileApps


```csharp
bool FileApps { get; }
```


The engine opens an app whose `url` is a `file:` URL, reading the file from the disk of the machine the harness runs on.
The harness checks every file navigation against that disk, so an engine whose surface may read another one must not declare this, or
must refuse such a target in `ValidateApp` ([FileApp](/api/E2E.Engine/EngineAppCheckInfo/#E2E_Engine_EngineAppCheckInfo_FileApp)). Without it the harness refuses a
file `app.url` at config load. Upstream's `Engine.fileApps`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_IEngine_GrantsAllPermissions_"></a>
<a id="E2E_Engine_IEngine_GrantsAllPermissions"></a>

### GrantsAllPermissions


```csharp
bool GrantsAllPermissions { get; }
```


The engine grants `app.permissions: 'all'`, which reaches it as [AllPermissions](/api/E2E.Engine/EngineStartOptions/#E2E_Engine_EngineStartOptions_AllPermissions). Without it the harness
refuses `'all'` at config load, so an engine that predates the value never receives it. Upstream's `EngineHandle.grantsAllPermissions`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_IEngine_LaunchArgumentValues_"></a>
<a id="E2E_Engine_IEngine_LaunchArgumentValues"></a>

### LaunchArgumentValues


```csharp
bool LaunchArgumentValues { get; }
```


Whether the engine reads the object form of `app.launchArguments` from [LaunchArgumentValues](/api/E2E.Engine/EngineAppInfo/#E2E_Engine_EngineAppInfo_LaunchArgumentValues) (upstream's
`EngineHandle.launchArgumentValues`). Without it the harness refuses an object at config load, and the engine only ever receives a list in
[LaunchArguments](/api/E2E.Engine/EngineAppInfo/#E2E_Engine_EngineAppInfo_LaunchArguments). The default is false, so an engine that predates the object form keeps compiling.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

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

<a id="E2E_Engine_IEngine_ValidateApp_"></a>
<a id="E2E_Engine_IEngine_ValidateApp_E2E_Engine_EngineAppDeclaration_E2E_Engine_EngineAppCheckInfo_"></a>

### ValidateApp(EngineAppDeclaration, EngineAppCheckInfo)


```csharp
void ValidateApp(EngineAppDeclaration app, EngineAppCheckInfo info)
```


Checks the app a target declares against what this engine can drive, at config load, synchronously: throw `INVALID_CONFIG`
naming [TargetName](/api/E2E.Engine/EngineAppCheckInfo/#E2E_Engine_EngineAppCheckInfo_TargetName) for a field the platform needs and the target left out (a URL, a bundle id) or one
it cannot drive. The harness has already checked every field's shape. Upstream's optional `validateApp`; the default checks nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `app` | [EngineAppDeclaration](/api/E2E.Engine/EngineAppDeclaration/) |  |
| `info` | [EngineAppCheckInfo](/api/E2E.Engine/EngineAppCheckInfo/) |  |
