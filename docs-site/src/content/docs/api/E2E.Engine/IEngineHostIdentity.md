---
title: "IEngineHostIdentity"
description: "Who holds a target, for the lifecycle hooks: prepare, init, finish. Upstream's EngineHostIdentity (engine/contract.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngineHostIdentity"
  hidden: true
---

<a id="E2E_Engine_IEngineHostIdentity"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngineHostIdentity
```


Who holds a target, for the lifecycle hooks: prepare, init, finish. Upstream's `EngineHostIdentity` (`engine/contract.ts`).

**Derived:** [EngineFinishInfo](/api/E2E.Engine/EngineFinishInfo/), [EngineInitInfo](/api/E2E.Engine/EngineInitInfo/), [EnginePrepareInfo](/api/E2E.Engine/EnginePrepareInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_IEngineHostIdentity_HostId_"></a>
<a id="E2E_Engine_IEngineHostIdentity_HostId"></a>

### HostId


```csharp
string? HostId { get; }
```


Set when a `launch()` host holds the target: a token no other host has. Hosts name their targets alike, so an engine that names
what it holds after the target (a device session) adds this to keep hosts apart. Upstream's `hostId`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
