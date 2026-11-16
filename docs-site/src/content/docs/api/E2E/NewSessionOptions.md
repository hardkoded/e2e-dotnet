---
title: "NewSessionOptions"
description: "What NewSessionAsync is given. Upstream's NewSessionOptions (core/launch.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "NewSessionOptions"
  hidden: true
---

<a id="E2E_NewSessionOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record NewSessionOptions : IEquatable<NewSessionOptions>
```


What `NewSessionAsync` is given. Upstream's `NewSessionOptions` (`core/launch.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;NewSessionOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Properties

<a id="E2E_NewSessionOptions_State_"></a>
<a id="E2E_NewSessionOptions_State"></a>

### State


```csharp
public SessionState? State { get; init; }
```


A state [SaveStateAsync(CancellationToken)](/api/E2E/Session/#E2E_Session_SaveStateAsync_System_Threading_CancellationToken_) captured, or `ImportAsync` read, on a host of the same engine, platform, and app.

**Value:** [SessionState](/api/E2E/SessionState/)
