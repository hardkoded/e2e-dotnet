---
title: "EnginePrepareInfo"
description: "The facts handed to an engine's prepare (CancellationToken)), once per run and target in the runner process, before any worker exists. Upstream's EnginePrepa..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EnginePrepareInfo"
  hidden: true
---

<a id="E2E_Engine_EnginePrepareInfo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EnginePrepareInfo : IEquatable<EnginePrepareInfo>
```


The facts handed to an engine's prepare (`CancellationToken)`), once per run and target in the runner
process, before any worker exists. Upstream's `EnginePrepareInfo`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EnginePrepareInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L217)

## Properties

<a id="E2E_Engine_EnginePrepareInfo_Env_"></a>
<a id="E2E_Engine_EnginePrepareInfo_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment: what every worker is started with. A host may hand the run an environment other than the process's own,
so anything provisioned here that a worker later looks up by environment must be resolved against this, not the process
environment.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L237)

<a id="E2E_Engine_EnginePrepareInfo_Log_"></a>
<a id="E2E_Engine_EnginePrepareInfo_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


Reports one line of progress. A run streams it as a notice to the reporter; the standalone host of `e2e explore` and
`e2e mcp` has no reporter, so it drops the line.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L249)

<a id="E2E_Engine_EnginePrepareInfo_ProjectRoot_"></a>
<a id="E2E_Engine_EnginePrepareInfo_ProjectRoot"></a>

### ProjectRoot


```csharp
public string? ProjectRoot { get; init; }
```


The directory relative paths in the config resolve against: where an engine resolves a relative option, such as a build to install.
Null leaves it to the working directory. Upstream's `projectRoot`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L243)

<a id="E2E_Engine_EnginePrepareInfo_RunId_"></a>
<a id="E2E_Engine_EnginePrepareInfo_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


The run id.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L220)

<a id="E2E_Engine_EnginePrepareInfo_Slots_"></a>
<a id="E2E_Engine_EnginePrepareInfo_Slots"></a>

### Slots


```csharp
public required int Slots { get; init; }
```


The worker slots the run will start for this target, `0` to [Slots](#E2E_Engine_EnginePrepareInfo_Slots) minus one. Provision for exactly these slots
(boot that many devices of a pool); the workers then find them ready. A standalone attempt, as `e2e explore` and
`e2e mcp` open, prepares one slot.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L230)

<a id="E2E_Engine_EnginePrepareInfo_TargetName_"></a>
<a id="E2E_Engine_EnginePrepareInfo_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


The target being prepared.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L223)
