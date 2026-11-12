---
title: "TracePosition"
description: "Upstream's TracePosition: the place of a target among the Of controls its description matched, counted from zero in document order. Strict number handling: a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TracePosition"
  hidden: true
---

<a id="E2E_TracePosition"></a>

Namespace: [E2E](/api/E2E/)


```csharp
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class TracePosition
```


Upstream's `TracePosition`: the place of a target among the [Of](#E2E_TracePosition_Of) controls its description matched, counted from zero in
document order. Strict number handling: a count stored as text is not a count.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_TracePosition_Index_"></a>
<a id="E2E_TracePosition_Index"></a>

### Index


```csharp
public int Index { get; set; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_TracePosition_Of_"></a>
<a id="E2E_TracePosition_Of"></a>

### Of


```csharp
public int Of { get; set; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
