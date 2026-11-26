---
title: "WireCleanup"
description: "An engine cleanup context on the wire. Upstream's WireCleanup."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireCleanup"
  hidden: true
---

<a id="E2E_Engine_Protocol_WireCleanup"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WireCleanup : IEquatable<WireCleanup>
```


An engine cleanup context on the wire. Upstream's `WireCleanup`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WireCleanup&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WireCleanup__ctor_"></a>
<a id="E2E_Engine_Protocol_WireCleanup__ctor_E2E_Engine_Protocol_WireSignal_System_Double_"></a>

### WireCleanup(WireSignal, double)


```csharp
public WireCleanup(WireSignal Signal, double TimeoutMs)
```


An engine cleanup context on the wire. Upstream's `WireCleanup`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Signal` | [WireSignal](/api/E2E.Engine.Protocol/WireSignal/) |  |
| `TimeoutMs` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Engine_Protocol_WireCleanup_Signal_"></a>
<a id="E2E_Engine_Protocol_WireCleanup_Signal"></a>

### Signal


```csharp
public WireSignal Signal { get; init; }
```


**Value:** [WireSignal](/api/E2E.Engine.Protocol/WireSignal/)

<a id="E2E_Engine_Protocol_WireCleanup_TimeoutMs_"></a>
<a id="E2E_Engine_Protocol_WireCleanup_TimeoutMs"></a>

### TimeoutMs


```csharp
public double TimeoutMs { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)
