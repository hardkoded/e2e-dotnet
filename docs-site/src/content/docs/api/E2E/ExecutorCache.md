---
title: "ExecutorCache"
description: "Trace-cache participation for an executor's steps. Upstream's cache: 'inherit' | 'off'."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorCache"
  hidden: true
---

<a id="E2E_ExecutorCache"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ExecutorCache
```


Trace-cache participation for an executor's steps. Upstream's `cache: 'inherit' | 'off'`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L53)

## Fields

<a id="E2E_ExecutorCache_Inherit"></a>

### Inherit


```csharp
Inherit = 0
```


Follows the configured cache mode.

**Value:** [ExecutorCache](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L56)

<a id="E2E_ExecutorCache_Off"></a>

### Off


```csharp
Off = 1
```


Never replays a cached trace for the executor's steps and records none, so every step reaches
`CancellationToken)`. For an executor whose own history or memory is the source of truth.

**Value:** [ExecutorCache](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L62)
