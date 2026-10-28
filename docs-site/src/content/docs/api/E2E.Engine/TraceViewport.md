---
title: "TraceViewport"
description: "Upstream's TraceViewport: the size of the viewport a capture was taken in, in CSS pixels."
editUrl: false
prev: false
next: false
sidebar:
  label: "TraceViewport"
  hidden: true
---

<a id="E2E_Engine_TraceViewport"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record TraceViewport : IEquatable<TraceViewport>
```


Upstream's `TraceViewport`: the size of the viewport a capture was taken in, in CSS pixels.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TraceViewport&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L45)

## Constructors

<a id="E2E_Engine_TraceViewport__ctor_"></a>
<a id="E2E_Engine_TraceViewport__ctor_System_Int32_System_Int32_"></a>

### TraceViewport(int, int)


```csharp
public TraceViewport(int Width, int Height)
```


Upstream's `TraceViewport`: the size of the viewport a capture was taken in, in CSS pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L45)

## Properties

<a id="E2E_Engine_TraceViewport_Height_"></a>
<a id="E2E_Engine_TraceViewport_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L45)

<a id="E2E_Engine_TraceViewport_Width_"></a>
<a id="E2E_Engine_TraceViewport_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L45)
