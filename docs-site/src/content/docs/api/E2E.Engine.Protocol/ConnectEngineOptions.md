---
title: "ConnectEngineOptions"
description: "Options of ConnectEngine(ConnectEngineOptions). Upstream's ConnectEngineOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "ConnectEngineOptions"
  hidden: true
---

<a id="E2E_Engine_Protocol_ConnectEngineOptions"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record ConnectEngineOptions : IEquatable<ConnectEngineOptions>
```


Options of [ConnectEngine(ConnectEngineOptions)](/api/E2E.Engine.Protocol/ProtocolClient/#E2E_Engine_Protocol_ProtocolClient_ConnectEngine_E2E_Engine_Protocol_ConnectEngineOptions_). Upstream's `ConnectEngineOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ConnectEngineOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Client.cs)

## Properties

<a id="E2E_Engine_Protocol_ConnectEngineOptions_Fixtures_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_Fixtures"></a>

### Fixtures


```csharp
public IReadOnlyDictionary<string, Delegate>? Fixtures { get; init; }
```


Fixtures this process contributes; see [Fixtures](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_Fixtures). They run on the client, beside the test.

**Value:** [IReadOnlyDictionary&lt;string, Delegate&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_InitializationOptions_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_InitializationOptions"></a>

### InitializationOptions


```csharp
public JsonNode? InitializationOptions { get; init; }
```


Engine options the server receives in `initialize`, as JSON.

**Value:** [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_Manifest_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_Manifest"></a>

### Manifest


```csharp
public required EngineManifest Manifest { get; init; }
```


What the server is: its name, version, platform, and the methods it answers. The harness reads it at config load; the server's own
manifest from `initialize` must match it.

**Value:** [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_ResolveLocators_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_ResolveLocators"></a>

### ResolveLocators


```csharp
public bool ResolveLocators { get; init; }
```


Answers locators on the client for a server without `engine/locate`: each `locate` observes and resolves the expression over
the tree with the reference semantics (`ResolveExpression`), so a server only has to produce a tree.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_Secrets_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_Secrets"></a>

### Secrets


```csharp
public IReadOnlyList<Secret>? Secrets { get; init; }
```


The `Secrets.Get` handles the server may resolve; see [Secrets](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_Secrets).

**Value:** [IReadOnlyList&lt;Secret&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_Session_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_Session"></a>

### Session


```csharp
public EngineSessionSpec? Session { get; init; }
```


Session hooks this process answers instead of the server, such as a relaunch the host makes from outside a device. Each one wins over
the server's method of the same name.

**Value:** [EngineSessionSpec](/api/E2E.Engine/EngineSessionSpec/)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_Transport_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_Transport"></a>

### Transport


```csharp
public required IEngineTransport Transport { get; init; }
```


How to reach the server: [SpawnTransport](/api/E2E.Engine.Protocol/SpawnTransport/), [SocketTransport](/api/E2E.Engine.Protocol/SocketTransport/), or [LoopbackTransport](/api/E2E.Engine.Protocol/LoopbackTransport/).

**Value:** [IEngineTransport](/api/E2E.Engine.Protocol/IEngineTransport/)

<a id="E2E_Engine_Protocol_ConnectEngineOptions_ValidateApp_"></a>
<a id="E2E_Engine_Protocol_ConnectEngineOptions_ValidateApp"></a>

### ValidateApp


```csharp
public Action<EngineAppDeclaration, EngineAppCheckInfo>? ValidateApp { get; init; }
```


Checks the target's app at config load, on the client; see [ValidateApp](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_ValidateApp).

**Value:** [Action&lt;EngineAppDeclaration, EngineAppCheckInfo&gt;](https://learn.microsoft.com/dotnet/api/system.action-2)
