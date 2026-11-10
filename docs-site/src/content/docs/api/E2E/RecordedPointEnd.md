---
title: "RecordedPointEnd"
description: "Upstream's TracePointEnd: a recorded point and, when a listed node with a durable descriptor contained it, its place inside that node's box."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedPointEnd"
  hidden: true
---

<a id="E2E_RecordedPointEnd"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class RecordedPointEnd
```


Upstream's `TracePointEnd`: a recorded point and, when a listed node with a durable descriptor contained it, its place inside that node's box.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_RecordedPointEnd_Point_"></a>
<a id="E2E_RecordedPointEnd_Point"></a>

### Point


```csharp
public Point? Point { get; set; }
```


The point the end was placed at. Null when a stored end lost it: upstream's `readPointEnd` then reads the end as undefined, so
`ReadTraceEntry(CacheEntry)` makes the entry a miss, where a 0,0 default would replay a point nobody recorded.

**Value:** `Point?`

<a id="E2E_RecordedPointEnd_Under_"></a>
<a id="E2E_RecordedPointEnd_Under"></a>

### Under


```csharp
public RecordedPointPlace? Under { get; set; }
```


**Value:** [RecordedPointPlace](/api/E2E/RecordedPointPlace/)
