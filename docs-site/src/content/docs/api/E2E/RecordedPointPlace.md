---
title: "RecordedPointPlace"
description: "Upstream's within of a TracePointEnd: the node a bare point sat in, and the point's place inside its box. Upstream types it inline ({ target, fx, fy }); C# h..."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedPointPlace"
  hidden: true
---

<a id="E2E_RecordedPointPlace"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class RecordedPointPlace
```


Upstream's `within` of a `TracePointEnd`: the node a bare point sat in, and the point's place inside its box. Upstream types it
inline (`{ target, fx, fy }`); C# has no inline object type, so it is a class (.NET-only name). The property that holds it is
`Under`, because [Within](/api/E2E/RecordedAction/#E2E_RecordedAction_Within) already names a container key here.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_RecordedPointPlace_Fx_"></a>
<a id="E2E_RecordedPointPlace_Fx"></a>

### Fx


```csharp
public double Fx { get; set; }
```


The point's place across the node's box, 0 at the left edge and 1 at the right.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_RecordedPointPlace_Fy_"></a>
<a id="E2E_RecordedPointPlace_Fy"></a>

### Fy


```csharp
public double Fy { get; set; }
```


The point's place down the node's box, 0 at the top edge and 1 at the bottom.

**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_RecordedPointPlace_Target_"></a>
<a id="E2E_RecordedPointPlace_Target"></a>

### Target


```csharp
public RecordedTarget? Target { get; set; }
```


How a replay re-finds the node: its role, label, test id and container key.

**Value:** [RecordedTarget](/api/E2E/RecordedTarget/)
