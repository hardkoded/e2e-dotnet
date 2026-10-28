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
public sealed record EngineInitInfo : IEquatable<EngineInitInfo>
```


What a worker's init is given, once per worker, before its first attempt. Upstream's `EngineInitInfo`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineInitInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L329)

## Properties

<a id="E2E_Engine_EngineInitInfo_Env_"></a>
<a id="E2E_Engine_EngineInitInfo_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment with what prepare left in it.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L339)

<a id="E2E_Engine_EngineInitInfo_Log_"></a>
<a id="E2E_Engine_EngineInitInfo_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L341)

<a id="E2E_Engine_EngineInitInfo_RunId_"></a>
<a id="E2E_Engine_EngineInitInfo_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L331)

<a id="E2E_Engine_EngineInitInfo_TargetName_"></a>
<a id="E2E_Engine_EngineInitInfo_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L333)

<a id="E2E_Engine_EngineInitInfo_WorkerSlot_"></a>
<a id="E2E_Engine_EngineInitInfo_WorkerSlot"></a>

### WorkerSlot


```csharp
public required int WorkerSlot { get; init; }
```


The worker slot this worker serves, `0` to the target's workers minus one.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L336)
