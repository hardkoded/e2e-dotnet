---
title: "EngineSessions"
description: "Upstream's createEngineSession (engine/session.ts): the engine-to-session adapter. One adapter per attempt satisfies ITargetSession over the engine contract,..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineSessions"
  hidden: true
---

<a id="E2E_Engine_EngineSessions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class EngineSessions
```


Upstream's `createEngineSession` (`engine/session.ts`): the engine-to-session adapter. One adapter per attempt satisfies
[ITargetSession](/api/E2E.Engine/ITargetSession/) over the engine contract, and is the single seam every engine call crosses. A capability the engine does not
declare fails loud with `UNSUPPORTED_CAPABILITY` at the moment of use, a `press` key outside the grammar fails with
`INVALID_ARGUMENT` before any engine sees it, and anything an engine throws that is not an `EngineException` is normalized to a
non-retryable `ENGINE_FAILURE`. .NET-only holder name: C# has no free functions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Session.cs)

## Fields

<a id="E2E_Engine_EngineSessions_MaxLocatedRefs"></a>

### MaxLocatedRefs


```csharp
public const int MaxLocatedRefs = 2048
```


Located nodes are pruned oldest-first past this bound, so the map cannot grow unboundedly.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

<a id="E2E_Engine_EngineSessions_CreateEngineSession_"></a>
<a id="E2E_Engine_EngineSessions_CreateEngineSession_E2E_Engine_EngineSessionOptions_"></a>

### CreateEngineSession(EngineSessionOptions)


```csharp
public static ITargetSession CreateEngineSession(EngineSessionOptions options)
```


Builds one session over an engine. Revisions are minted per adapter, so refs can never leak across attempts.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [EngineSessionOptions](/api/E2E.Engine/EngineSessionOptions/) |  |

**Returns:** [ITargetSession](/api/E2E.Engine/ITargetSession/)
