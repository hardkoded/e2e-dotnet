---
title: "ActResult"
description: "ActResult in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActResult"
  hidden: true
---

<a id="E2E_ActResult"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ActResult
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L999)

## Properties

<a id="E2E_ActResult_Actions_"></a>
<a id="E2E_ActResult_Actions"></a>

### Actions


```csharp
public int Actions { get; init; }
```


Actions the step performed, replayed or live, counting ones that failed.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1013)

<a id="E2E_ActResult_Cache_"></a>
<a id="E2E_ActResult_Cache"></a>

### Cache


```csharp
public CacheInfo? Cache { get; init; }
```


Null when caching is off. `self-finalized` finished with no model call,
`agent-concluded` replayed then handed off, `missed` ran live from the start.

**Value:** [CacheInfo](/api/E2E/CacheInfo/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1007)

<a id="E2E_ActResult_ModelCalls_"></a>
<a id="E2E_ActResult_ModelCalls"></a>

### ModelCalls


```csharp
public int ModelCalls { get; init; }
```


Model calls the step spent. 0 when a replay finished it.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1010)

<a id="E2E_ActResult_Summary_"></a>
<a id="E2E_ActResult_Summary"></a>

### Summary


```csharp
public required string Summary { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1001)
