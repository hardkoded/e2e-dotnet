---
title: "ConnectionRole"
description: "What a connection serves: a run's provisioning, a worker, or e2e doctor's checks. Upstream's ConnectionRole."
editUrl: false
prev: false
next: false
sidebar:
  label: "ConnectionRole"
  hidden: true
---

<a id="E2E_Engine_Protocol_ConnectionRole"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public static class ConnectionRole
```


What a connection serves: a run's provisioning, a worker, or `e2e doctor`'s checks. Upstream's `ConnectionRole`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Fields

<a id="E2E_Engine_Protocol_ConnectionRole_Doctor"></a>

### Doctor


```csharp
public const string Doctor = "doctor"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_ConnectionRole_Run"></a>

### Run


```csharp
public const string Run = "run"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_ConnectionRole_Worker"></a>

### Worker


```csharp
public const string Worker = "worker"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
