---
title: "TransportContext"
description: "What a transport knows when it opens a connection. Upstream's TransportContext (engine/protocol/transport.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TransportContext"
  hidden: true
---

<a id="E2E_Engine_Protocol_TransportContext"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record TransportContext : IEquatable<TransportContext>
```


What a transport knows when it opens a connection. Upstream's `TransportContext` (`engine/protocol/transport.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TransportContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Properties

<a id="E2E_Engine_Protocol_TransportContext_App_"></a>
<a id="E2E_Engine_Protocol_TransportContext_App"></a>

### App


```csharp
public EngineAppInfo? App { get; init; }
```


The app the target declares, as `prepare`, `init`, and `doctor` receive it: what a transport that provisions a machine
installs there (a build at [AppPath](/api/E2E.Engine/EngineAppInfo/#E2E_Engine_EngineAppInfo_AppPath), relative to [Cwd](#E2E_Engine_Protocol_TransportContext_Cwd)), since a server elsewhere cannot read the
project's files. Null for a connection opened outside those hooks.

**Value:** [EngineAppInfo](/api/E2E.Engine/EngineAppInfo/)

<a id="E2E_Engine_Protocol_TransportContext_Cwd_"></a>
<a id="E2E_Engine_Protocol_TransportContext_Cwd"></a>

### Cwd


```csharp
public required string Cwd { get; init; }
```


The project root, where relative paths resolve.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_TransportContext_Env_"></a>
<a id="E2E_Engine_Protocol_TransportContext_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The environment the harness would start a worker with.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_Protocol_TransportContext_Role_"></a>
<a id="E2E_Engine_Protocol_TransportContext_Role"></a>

### Role


```csharp
public required string Role { get; init; }
```


The connection's role; see [Role](/api/E2E.Engine.Protocol/InitializeParams/#E2E_Engine_Protocol_InitializeParams_Role).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_TransportContext_Signal_"></a>
<a id="E2E_Engine_Protocol_TransportContext_Signal"></a>

### Signal


```csharp
public CancellationToken Signal { get; init; }
```


Cancels when opening must stop; it never cancels once [OpenAsync(TransportContext)](/api/E2E.Engine.Protocol/IEngineTransport/#E2E_Engine_Protocol_IEngineTransport_OpenAsync_E2E_Engine_Protocol_TransportContext_) has returned.

**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)
