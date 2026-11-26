---
title: "SpawnTransportOptions"
description: "Options of SpawnTransport. Upstream's SpawnTransportOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "SpawnTransportOptions"
  hidden: true
---

<a id="E2E_Engine_Protocol_SpawnTransportOptions"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record SpawnTransportOptions : IEquatable<SpawnTransportOptions>
```


Options of [SpawnTransport](/api/E2E.Engine.Protocol/SpawnTransport/). Upstream's `SpawnTransportOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SpawnTransportOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Transport.cs)

## Properties

<a id="E2E_Engine_Protocol_SpawnTransportOptions_Args_"></a>
<a id="E2E_Engine_Protocol_SpawnTransportOptions_Args"></a>

### Args


```csharp
public IReadOnlyList<string>? Args { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_SpawnTransportOptions_Command_"></a>
<a id="E2E_Engine_Protocol_SpawnTransportOptions_Command"></a>

### Command


```csharp
public required string Command { get; init; }
```


The server executable.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_SpawnTransportOptions_Cwd_"></a>
<a id="E2E_Engine_Protocol_SpawnTransportOptions_Cwd"></a>

### Cwd


```csharp
public string? Cwd { get; init; }
```


Working directory; defaults to the project root.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_SpawnTransportOptions_Env_"></a>
<a id="E2E_Engine_Protocol_SpawnTransportOptions_Env"></a>

### Env


```csharp
public IReadOnlyDictionary<string, string>? Env { get; init; }
```


Variables added to the worker's environment for the server.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
