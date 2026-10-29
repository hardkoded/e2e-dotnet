---
title: "CacheEntry"
description: "A recording of one verified act. Its file name is the hash of a trace cache key (TraceCacheKeyHash): the cache schema, the replay policy version, the project..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CacheEntry"
  hidden: true
---

<a id="E2E_CacheEntry"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CacheEntry
```


A recording of one verified `act`. Its file name is the hash of a trace cache key (`TraceCacheKeyHash`):
the cache schema, the replay policy version, the project, the test, the target, the engine's name and contract version, the app's
identity, the instruction's and params' digests, the agent's name, a digest of the agent's redacted context, and which repeat of
that same call in the attempt it is. The model id and the engine's package version are not part of the key.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L23)

## Properties

<a id="E2E_CacheEntry_Actions_"></a>
<a id="E2E_CacheEntry_Actions"></a>

### Actions


```csharp
public List<RecordedAction> Actions { get; set; }
```


**Value:** [List&lt;RecordedAction&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L62)

<a id="E2E_CacheEntry_Agent_"></a>
<a id="E2E_CacheEntry_Agent"></a>

### Agent


```csharp
public string? Agent { get; set; }
```


Name of the configured agent the step ran with. A secret value in it is masked.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L42)

<a id="E2E_CacheEntry_Appeared_"></a>
<a id="E2E_CacheEntry_Appeared"></a>

### Appeared


```csharp
public List<RecordedTarget> Appeared { get; set; }
```


Nodes on screen when the step passed and not when it began (`Anchors`).

**Value:** [List&lt;RecordedTarget&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L65)

<a id="E2E_CacheEntry_CallIndex_"></a>
<a id="E2E_CacheEntry_CallIndex"></a>

### CallIndex


```csharp
public int? CallIndex { get; set; }
```


Zero-based repeat of the step among identical acts in the attempt.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L39)

<a id="E2E_CacheEntry_EndRoute_"></a>
<a id="E2E_CacheEntry_EndRoute"></a>

### EndRoute


```csharp
public string? EndRoute { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L60)

<a id="E2E_CacheEntry_EndWaitMs_"></a>
<a id="E2E_CacheEntry_EndWaitMs"></a>

### EndWaitMs


```csharp
public int? EndWaitMs { get; set; }
```


Upstream's `endWaitMs`: how long the recorded run took from its last action to its passing verdict, plus a margin. A replay
waits up to this long for the recorded end state to show: the recorded run waited for its effect too (a report that takes half a
minute), and the click alone proves nothing. Null on an entry recorded before it was stored.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L75)

<a id="E2E_CacheEntry_Engine_"></a>
<a id="E2E_CacheEntry_Engine"></a>

### Engine


```csharp
public string? Engine { get; set; }
```


The engine platform the step ran on. Null on an entry recorded before it was stored.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L33)

<a id="E2E_CacheEntry_Gone_"></a>
<a id="E2E_CacheEntry_Gone"></a>

### Gone


```csharp
public List<RecordedTarget> Gone { get; set; }
```


Nodes on screen when the step began and gone when it passed.

**Value:** [List&lt;RecordedTarget&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L68)

<a id="E2E_CacheEntry_Instruction_"></a>
<a id="E2E_CacheEntry_Instruction"></a>

### Instruction


```csharp
public string? Instruction { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L30)

<a id="E2E_CacheEntry_KeyedBy_"></a>
<a id="E2E_CacheEntry_KeyedBy"></a>

### KeyedBy


```csharp
[JsonConverter(typeof(KeyContextConverter))]
public TraceKeyContext? KeyedBy { get; set; }
```


Upstream's `keyedBy`: the parts of the key outside the step itself, as they were when it was recorded. Provenance, never read
by a replay: when a step's key no longer finds this entry, comparing it with the run's says what changed. Null on an older entry.

**Value:** [TraceKeyContext](/api/E2E/TraceKeyContext/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L55)

<a id="E2E_CacheEntry_ParamsDigest_"></a>
<a id="E2E_CacheEntry_ParamsDigest"></a>

### ParamsDigest


```csharp
public string? ParamsDigest { get; set; }
```


SHA-256 of the params as the key holds them, a placeholder for each unique value.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L36)

<a id="E2E_CacheEntry_RecordedBy_"></a>
<a id="E2E_CacheEntry_RecordedBy"></a>

### RecordedBy


```csharp
[JsonConverter(typeof(OptionalTextConverter))]
public string? RecordedBy { get; set; }
```


The configured model that recorded the step, as [Name](/api/E2E/IAgentModel/#E2E_IAgentModel_Name) spells it (provider and model id together).
Null on an entry recorded before it was stored, or by an agent with no configured model. It never keys the entry.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L48)

<a id="E2E_CacheEntry_Route_"></a>
<a id="E2E_CacheEntry_Route"></a>

### Route


```csharp
public string? Route { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L58)

<a id="E2E_CacheEntry_Schema_"></a>
<a id="E2E_CacheEntry_Schema"></a>

### Schema


```csharp
public int Schema { get; set; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L25)

<a id="E2E_CacheEntry_Test_"></a>
<a id="E2E_CacheEntry_Test"></a>

### Test


```csharp
public string? Test { get; set; }
```


The test title. A secret value in it is masked.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L28)
