---
title: "EngineIsolation"
description: "Upstream's EngineIsolation (engine/index.ts): how long the state behind an engine's fixtures, app, and screen lives."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineIsolation"
  hidden: true
---

<a id="E2E_Engine_EngineIsolation"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class EngineIsolation
```


Upstream's `EngineIsolation` (`engine/index.ts`): how long the state behind an engine's fixtures, `app`, and `screen` lives.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Fields

<a id="E2E_Engine_EngineIsolation_Attempt"></a>

### Attempt


```csharp
public const string Attempt = "attempt"
```


The state ends with the attempt; the default.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineIsolation_Worker"></a>

### Worker


```csharp
public const string Worker = "worker"
```


The state outlives an attempt and lives as long as the worker.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
