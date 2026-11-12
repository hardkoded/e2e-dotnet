---
title: "EngineCleanupContext"
description: "Upstream's EngineCleanupContext: the signal and the cleanup budget the harness hands an engine's cleanup hooks."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineCleanupContext"
  hidden: true
---

<a id="E2E_Engine_EngineCleanupContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineCleanupContext : IEquatable<EngineCleanupContext>
```


Upstream's `EngineCleanupContext`: the signal and the cleanup budget the harness hands an engine's cleanup hooks.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineCleanupContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_EngineCleanupContext__ctor_"></a>
<a id="E2E_Engine_EngineCleanupContext__ctor_System_Threading_CancellationToken_System_TimeSpan_"></a>

### EngineCleanupContext(CancellationToken, TimeSpan)


```csharp
public EngineCleanupContext(CancellationToken Signal, TimeSpan Timeout)
```


Upstream's `EngineCleanupContext`: the signal and the cleanup budget the harness hands an engine's cleanup hooks.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Signal` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |
| `Timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

## Properties

<a id="E2E_Engine_EngineCleanupContext_Signal_"></a>
<a id="E2E_Engine_EngineCleanupContext_Signal"></a>

### Signal


```csharp
public CancellationToken Signal { get; init; }
```


**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

<a id="E2E_Engine_EngineCleanupContext_Timeout_"></a>
<a id="E2E_Engine_EngineCleanupContext_Timeout"></a>

### Timeout


```csharp
public TimeSpan Timeout { get; init; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
