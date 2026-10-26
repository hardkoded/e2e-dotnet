---
title: "CacheConfig"
description: "CacheConfig in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "CacheConfig"
  hidden: true
---

<a id="E2E_CacheConfig"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CacheConfig
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L865)

## Properties

<a id="E2E_CacheConfig_Directory_"></a>
<a id="E2E_CacheConfig_Directory"></a>

### Directory


```csharp
public string Directory { get; init; }
```


Absolute cache directory. `cache.dir` resolves against the config directory; the default is `.e2e/cache`. A non-blank `E2E_CACHE_DIR` replaces both.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L871)

<a id="E2E_CacheConfig_Mode_"></a>
<a id="E2E_CacheConfig_Mode"></a>

### Mode


```csharp
public CacheMode Mode { get; init; }
```


Unset, it is [ReadWrite](/api/E2E/CacheMode/#E2E_CacheMode_ReadWrite), or [ReadOnly](/api/E2E/CacheMode/#E2E_CacheMode_ReadOnly) in CI.

**Value:** [CacheMode](/api/E2E/CacheMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L868)

<a id="E2E_CacheConfig_Strict_"></a>
<a id="E2E_CacheConfig_Strict"></a>

### Strict


```csharp
public bool Strict { get; init; }
```


When true, a recording that no longer matches fails with `REPLAY_STALE` instead of running live.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L874)
