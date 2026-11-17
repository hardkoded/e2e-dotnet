---
title: "CachingActOptions"
description: "ActOptions for ActAsync: each call names the recording it replays and records, or runs live. Upstream's CachingActOptions (core/agent.ts), which TypeScript t..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CachingActOptions"
  hidden: true
---

<a id="E2E_CachingActOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CachingActOptions : ActOptions
```


[ActOptions](/api/E2E/ActOptions/) for `ActAsync`: each call names the recording it replays and records, or runs live. Upstream's
`CachingActOptions` (`core/agent.ts`), which TypeScript types as a union; C# holds both members, and the agent refuses the one that
does not fit it at runtime. [Agent](/api/E2E/ActOptions/#E2E_ActOptions_Agent) is `INVALID_ARGUMENT`: create another agent for another configuration.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActOptions](/api/E2E/ActOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/CoreAgent.cs)

## Properties

<a id="E2E_CachingActOptions_Cache_"></a>
<a id="E2E_CachingActOptions_Cache"></a>

### Cache


```csharp
public bool? Cache { get; init; }
```


`false` runs this call live, neither replaying nor recording. Any other value is `INVALID_ARGUMENT`.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_CachingActOptions_CacheKey_"></a>
<a id="E2E_CachingActOptions_CacheKey"></a>

### CacheKey


```csharp
public string? CacheKey { get; init; }
```


The recording this call replays and records, unique within what the program does: `checkout`, `cart-best-coupon`. The key is
this name, the instruction, its params, and how many times the session made the same call, so a reworded instruction records afresh.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
