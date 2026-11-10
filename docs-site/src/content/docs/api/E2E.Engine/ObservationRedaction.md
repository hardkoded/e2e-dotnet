---
title: "ObservationRedaction"
description: "Upstream's Observation.redaction: the counts a capture's pixels are judged by."
editUrl: false
prev: false
next: false
sidebar:
  label: "ObservationRedaction"
  hidden: true
---

<a id="E2E_Engine_ObservationRedaction"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ObservationRedaction : IEquatable<ObservationRedaction>
```


Upstream's `Observation.redaction`: the counts a capture's pixels are judged by.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ObservationRedaction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_ObservationRedaction__ctor_"></a>
<a id="E2E_Engine_ObservationRedaction__ctor_System_Int32_System_Int32_"></a>

### ObservationRedaction(int, int)


```csharp
public ObservationRedaction(int SecureNodeCount, int MaskedRegionCount)
```


Upstream's `Observation.redaction`: the counts a capture's pixels are judged by.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `SecureNodeCount` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `MaskedRegionCount` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Engine_ObservationRedaction_MaskedRegionCount_"></a>
<a id="E2E_Engine_ObservationRedaction_MaskedRegionCount"></a>

### MaskedRegionCount


```csharp
public int MaskedRegionCount { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_ObservationRedaction_SecureNodeCount_"></a>
<a id="E2E_Engine_ObservationRedaction_SecureNodeCount"></a>

### SecureNodeCount


```csharp
public int SecureNodeCount { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
