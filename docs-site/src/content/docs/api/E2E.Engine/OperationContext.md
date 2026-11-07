---
title: "OperationContext"
description: "What an engine call carries about the call itself. Upstream's OperationContext. origin, runId and attemptId are members here: upstream's signal is the Cancel..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OperationContext"
  hidden: true
---

<a id="E2E_Engine_OperationContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record OperationContext : IEquatable<OperationContext>
```


What an engine call carries about the call itself. Upstream's `OperationContext`. `origin`, `runId` and `attemptId` are
members here: upstream's `signal` is the [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) every engine call takes, and `timeoutMs` is the per-call
timeout (such as `ObserveAsync`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;OperationContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/OperationContext.cs)

## Constructors

<a id="E2E_Engine_OperationContext__ctor_"></a>
<a id="E2E_Engine_OperationContext__ctor_E2E_Engine_OperationOrigin_"></a>

### OperationContext(OperationOrigin)


```csharp
public OperationContext(OperationOrigin Origin)
```


What an engine call carries about the call itself. Upstream's `OperationContext`. `origin`, `runId` and `attemptId` are
members here: upstream's `signal` is the [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) every engine call takes, and `timeoutMs` is the per-call
timeout (such as `ObserveAsync`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Origin` | [OperationOrigin](/api/E2E.Engine/OperationOrigin/) | Who asked. Upstream's `origin`. |

## Properties

<a id="E2E_Engine_OperationContext_Agent_"></a>
<a id="E2E_Engine_OperationContext_Agent"></a>

### Agent


```csharp
public static OperationContext Agent { get; }
```


The agent: an engine may wait for the transition to end first.

**Value:** [OperationContext](./)

<a id="E2E_Engine_OperationContext_AttemptId_"></a>
<a id="E2E_Engine_OperationContext_AttemptId"></a>

### AttemptId


```csharp
public string AttemptId { get; init; }
```


The attempt id: the one [AttemptId](/api/E2E.Engine/EngineStartOptions/#E2E_Engine_EngineStartOptions_AttemptId) named at start. Upstream's `attemptId`. Filled in like [RunId](#E2E_Engine_OperationContext_RunId).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_OperationContext_Origin_"></a>
<a id="E2E_Engine_OperationContext_Origin"></a>

### Origin


```csharp
public OperationOrigin Origin { get; init; }
```


Who asked. Upstream's `origin`.

**Value:** [OperationOrigin](/api/E2E.Engine/OperationOrigin/)

<a id="E2E_Engine_OperationContext_RunId_"></a>
<a id="E2E_Engine_OperationContext_RunId"></a>

### RunId


```csharp
public string RunId { get; init; }
```


The run id. Upstream's `runId`. The runtime fills it in before the call reaches the engine; [Test](#E2E_Engine_OperationContext_Test) and [Agent](#E2E_Engine_OperationContext_Agent)
carry none (.NET-only: they are shared values, and the properties are `init` with an empty default so a context built before they existed
still compiles).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_OperationContext_Test_"></a>
<a id="E2E_Engine_OperationContext_Test"></a>

### Test


```csharp
public static OperationContext Test { get; }
```


A test's own step: an engine may act without settling.

**Value:** [OperationContext](./)
