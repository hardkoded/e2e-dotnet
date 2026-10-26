---
title: "BrowserRequest"
description: "What the engine asks a provider for: one browser for one worker slot, or for one attempt, of a run."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserRequest"
  hidden: true
---

<a id="E2E_Engine_BrowserRequest"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserRequest : IEquatable<BrowserRequest>
```


What the engine asks a provider for: one browser for one worker slot, or for one attempt, of a run.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserRequest&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L41)

## Properties

<a id="E2E_Engine_BrowserRequest_AttemptId_"></a>
<a id="E2E_Engine_BrowserRequest_AttemptId"></a>

### AttemptId


```csharp
public string? AttemptId { get; init; }
```


Present only for a per-attempt lease.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L56)

<a id="E2E_Engine_BrowserRequest_Env_"></a>
<a id="E2E_Engine_BrowserRequest_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment: where a provider reads its token from, never the process environment.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L59)

<a id="E2E_Engine_BrowserRequest_Log_"></a>
<a id="E2E_Engine_BrowserRequest_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


Reports one line of progress to the run's reporter (a session URL to watch), from the runner and from a worker alike.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L62)

<a id="E2E_Engine_BrowserRequest_RunId_"></a>
<a id="E2E_Engine_BrowserRequest_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


The run id, for naming the session at the provider.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L44)

<a id="E2E_Engine_BrowserRequest_Slot_"></a>
<a id="E2E_Engine_BrowserRequest_Slot"></a>

### Slot


```csharp
public required int Slot { get; init; }
```


Worker slot the browser serves, `0` to `Slots - 1`. In [Worker](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Worker) scope [Slots](#E2E_Engine_BrowserRequest_Slots) leases are acquired at once.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L50)

<a id="E2E_Engine_BrowserRequest_Slots_"></a>
<a id="E2E_Engine_BrowserRequest_Slots"></a>

### Slots


```csharp
public required int Slots { get; init; }
```


Worker slots the run uses for this target, in all.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L53)

<a id="E2E_Engine_BrowserRequest_TargetName_"></a>
<a id="E2E_Engine_BrowserRequest_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


The target the browser serves.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L47)
