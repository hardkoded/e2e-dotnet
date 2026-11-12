---
title: "EngineDisposeContext"
description: "What DisposeAsync is given: the cleanup budget of one engine's dispose, and whether it may keep what it holds for a replacement worker. Upstream's EngineDisp..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineDisposeContext"
  hidden: true
---

<a id="E2E_Engine_EngineDisposeContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineDisposeContext : IEquatable<EngineDisposeContext>
```


What `DisposeAsync` is given: the cleanup budget of one engine's
dispose, and whether it may keep what it holds for a replacement worker. Upstream's `EngineDisposeContext` (`engine/index.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineDisposeContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IPreparableEngine.cs)

## Properties

<a id="E2E_Engine_EngineDisposeContext_CleanupTimeout_"></a>
<a id="E2E_Engine_EngineDisposeContext_CleanupTimeout"></a>

### CleanupTimeout


```csharp
public required TimeSpan CleanupTimeout { get; init; }
```


The cleanup budget the dispose shares.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Engine_EngineDisposeContext_ReuseSignal_"></a>
<a id="E2E_Engine_EngineDisposeContext_ReuseSignal"></a>

### ReuseSignal


```csharp
public CancellationToken? ReuseSignal { get; init; }
```


Upstream's `reuseSignal`: a token that is live (not canceled) permits the engine to keep the run-owned resources its workers hold
for a replacement, until the token is canceled or the dispose settles. Null or a canceled token requires the final cleanup, which is
what a run that retires its workers asks for.

**Value:** [CancellationToken?](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)
