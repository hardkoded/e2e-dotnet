---
title: "AgentCacheOptions"
description: "Where ActAsync keeps the flows it recorded, and whether it may write new ones. Upstream's AgentCacheOptions (core/agent.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentCacheOptions"
  hidden: true
---

<a id="E2E_AgentCacheOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AgentCacheOptions
```


Where `ActAsync` keeps the flows it recorded, and whether it may write new ones. Upstream's `AgentCacheOptions` (`core/agent.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/CoreAgent.cs)

## Properties

<a id="E2E_AgentCacheOptions_Dir_"></a>
<a id="E2E_AgentCacheOptions_Dir"></a>

### Dir


```csharp
public string? Dir { get; init; }
```


The file store's directory, from the [Cwd](/api/E2E/LaunchOptions/#E2E_LaunchOptions_Cwd) the host was launched with; default `.e2e/cache`. Ignored with [Store](#E2E_AgentCacheOptions_Store).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_AgentCacheOptions_Mode_"></a>
<a id="E2E_AgentCacheOptions_Mode"></a>

### Mode


```csharp
public CacheMode Mode { get; init; }
```


[ReadWrite](/api/E2E/CacheMode/#E2E_CacheMode_ReadWrite) (the default) replays and records; [ReadOnly](/api/E2E/CacheMode/#E2E_CacheMode_ReadOnly) replays and never writes. [Off](/api/E2E/CacheMode/#E2E_CacheMode_Off) is `INVALID_ARGUMENT`: leave [Cache](/api/E2E/CreateAgentOptions/#E2E_CreateAgentOptions_Cache) unset.

**Value:** [CacheMode](/api/E2E/CacheMode/)

<a id="E2E_AgentCacheOptions_Store_"></a>
<a id="E2E_AgentCacheOptions_Store"></a>

### Store


```csharp
public IStepCache? Store { get; init; }
```


A custom store in place of the file store, the seam a shared cache plugs into.

**Value:** [IStepCache](/api/E2E/IStepCache/)

<a id="E2E_AgentCacheOptions_Strict_"></a>
<a id="E2E_AgentCacheOptions_Strict"></a>

### Strict


```csharp
public bool Strict { get; init; }
```


Replays only: a recording that no longer replays fails its step with `REPLAY_STALE` instead of calling the model, and nothing is written.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
