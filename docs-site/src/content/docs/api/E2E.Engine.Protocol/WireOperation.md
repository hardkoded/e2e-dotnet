---
title: "WireOperation"
description: "An operation context on the wire. Upstream's WireOperation."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireOperation"
  hidden: true
---

<a id="E2E_Engine_Protocol_WireOperation"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WireOperation : IEquatable<WireOperation>
```


An operation context on the wire. Upstream's `WireOperation`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WireOperation&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WireOperation__ctor_"></a>
<a id="E2E_Engine_Protocol_WireOperation__ctor_E2E_Engine_Protocol_WireSignal_System_Double_System_String_System_String_System_String_"></a>

### WireOperation(WireSignal, double, string, string, string)


```csharp
public WireOperation(WireSignal Signal, double TimeoutMs, string RunId, string AttemptId, string Origin)
```


An operation context on the wire. Upstream's `WireOperation`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Signal` | [WireSignal](/api/E2E.Engine.Protocol/WireSignal/) |  |
| `TimeoutMs` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `RunId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `AttemptId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Origin` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_Protocol_WireOperation_AttemptId_"></a>
<a id="E2E_Engine_Protocol_WireOperation_AttemptId"></a>

### AttemptId


```csharp
public string AttemptId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WireOperation_Origin_"></a>
<a id="E2E_Engine_Protocol_WireOperation_Origin"></a>

### Origin


```csharp
public string Origin { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WireOperation_RunId_"></a>
<a id="E2E_Engine_Protocol_WireOperation_RunId"></a>

### RunId


```csharp
public string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WireOperation_Signal_"></a>
<a id="E2E_Engine_Protocol_WireOperation_Signal"></a>

### Signal


```csharp
public WireSignal Signal { get; init; }
```


**Value:** [WireSignal](/api/E2E.Engine.Protocol/WireSignal/)

<a id="E2E_Engine_Protocol_WireOperation_TimeoutMs_"></a>
<a id="E2E_Engine_Protocol_WireOperation_TimeoutMs"></a>

### TimeoutMs


```csharp
public double TimeoutMs { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)
