---
title: "EngineInitInfo"
description: "What a worker's init is given, once per worker, before its first attempt. Upstream's EngineInitInfo."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineInitInfo"
  hidden: true
---

<a id="E2E_Engine_EngineInitInfo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineInitInfo : IEngineHostIdentity, IEquatable<EngineInitInfo>
```


What a worker's init is given, once per worker, before its first attempt. Upstream's `EngineInitInfo`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEngineHostIdentity](/api/E2E.Engine/IEngineHostIdentity/), [IEquatable&lt;EngineInitInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_EngineInitInfo_App_"></a>
<a id="E2E_Engine_EngineInitInfo_App"></a>

### App


```csharp
public EngineAppInfo? App { get; init; }
```


The app the target declares, as [App](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_App). Upstream's `app`.

**Value:** [EngineAppInfo](/api/E2E.Engine/EngineAppInfo/)

<a id="E2E_Engine_EngineInitInfo_Env_"></a>
<a id="E2E_Engine_EngineInitInfo_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment with what prepare left in it.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_EngineInitInfo_Headed_"></a>
<a id="E2E_Engine_EngineInitInfo_Headed"></a>

### Headed


```csharp
public bool Headed { get; init; }
```


Whether the run asked for a visible surface (`--headed`). Upstream's `headed`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineInitInfo_HostId_"></a>
<a id="E2E_Engine_EngineInitInfo_HostId"></a>

### HostId


```csharp
public string? HostId { get; init; }
```


Set when a `launch()` host holds the target: a token no other host has. Hosts name their targets alike, so an engine that names
what it holds after the target (a device session) adds this to keep hosts apart. Upstream's `hostId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineInitInfo_Log_"></a>
<a id="E2E_Engine_EngineInitInfo_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_EngineInitInfo_ProjectRoot_"></a>
<a id="E2E_Engine_EngineInitInfo_ProjectRoot"></a>

### ProjectRoot


```csharp
public string? ProjectRoot { get; init; }
```


The directory relative paths in the config resolve against, as [ProjectRoot](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_ProjectRoot). Upstream's `projectRoot`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineInitInfo_RunId_"></a>
<a id="E2E_Engine_EngineInitInfo_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineInitInfo_TargetName_"></a>
<a id="E2E_Engine_EngineInitInfo_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineInitInfo_WorkerSlot_"></a>
<a id="E2E_Engine_EngineInitInfo_WorkerSlot"></a>

### WorkerSlot


```csharp
public required int WorkerSlot { get; init; }
```


The worker slot this worker serves, `0` to the target's workers minus one.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
