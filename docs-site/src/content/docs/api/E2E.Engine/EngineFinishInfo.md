---
title: "EngineFinishInfo"
description: "The facts handed to an engine's finish (CancellationToken)), once per run and target after the last worker is gone, with the cleanup budget of one hook. Upst..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineFinishInfo"
  hidden: true
---

<a id="E2E_Engine_EngineFinishInfo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineFinishInfo : IEquatable<EngineFinishInfo>
```


The facts handed to an engine's finish (`CancellationToken)`), once per run and target after the last worker is
gone, with the cleanup budget of one hook. Upstream's `EngineFinishInfo`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineFinishInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L305)

## Properties

<a id="E2E_Engine_EngineFinishInfo_CleanupTimeout_"></a>
<a id="E2E_Engine_EngineFinishInfo_CleanupTimeout"></a>

### CleanupTimeout


```csharp
public TimeSpan CleanupTimeout { get; init; }
```


The cleanup budget the host leaves this call: the finish hooks of every prepared target share one `cleanupTimeout`, so the
host hands each what is left of it when the call starts and cancels the call's token when it is spent. A hook that cannot finish in
time stops instead of hanging the run; one that ignores the token is abandoned at the deadline with `CLEANUP_TIMEOUT`.
Upstream's `timeoutMs`.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L325)

<a id="E2E_Engine_EngineFinishInfo_Env_"></a>
<a id="E2E_Engine_EngineFinishInfo_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment, the same prepare saw.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L314)

<a id="E2E_Engine_EngineFinishInfo_Log_"></a>
<a id="E2E_Engine_EngineFinishInfo_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


Reports one line of progress, as [Log](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_Log) does.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L317)

<a id="E2E_Engine_EngineFinishInfo_RunId_"></a>
<a id="E2E_Engine_EngineFinishInfo_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


The run id.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L308)

<a id="E2E_Engine_EngineFinishInfo_TargetName_"></a>
<a id="E2E_Engine_EngineFinishInfo_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


The target being finished.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L311)
