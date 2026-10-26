---
title: "BrowserLease"
description: "One leased browser, as CancellationToken) returns it and CancellationToken) gets it back. A provider may derive from it and keep further fields: release gets..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserLease"
  hidden: true
---

<a id="E2E_Engine_BrowserLease"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public class BrowserLease
```


One leased browser, as `CancellationToken)` returns it and
`CancellationToken)` gets it back. A provider may derive from it and keep further fields:
release gets that same object, while only the declared fields travel from the runner to the worker that attaches to it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L70)

## Properties

<a id="E2E_Engine_BrowserLease_CdpEndpoint_"></a>
<a id="E2E_Engine_BrowserLease_CdpEndpoint"></a>

### CdpEndpoint


```csharp
public string CdpEndpoint { get; init; }
```


`ws://`, `wss://`, or `http://` endpoint to attach to, over CDP unless [Protocol](#E2E_Engine_BrowserLease_Protocol) says otherwise.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L76)

<a id="E2E_Engine_BrowserLease_Id_"></a>
<a id="E2E_Engine_BrowserLease_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


The provider's handle on the lease (a session id), named in progress lines and handed back to release.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L73)

<a id="E2E_Engine_BrowserLease_Protocol_"></a>
<a id="E2E_Engine_BrowserLease_Protocol"></a>

### Protocol


```csharp
public BrowserProtocol Protocol { get; init; }
```


What [CdpEndpoint](#E2E_Engine_BrowserLease_CdpEndpoint) speaks. A [Playwright](/api/E2E.Engine/BrowserProtocol/#E2E_Engine_BrowserProtocol_Playwright) lease needs [Worker](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Worker)
scope and no [ReconnectEndpoint](#E2E_Engine_BrowserLease_ReconnectEndpoint): its connection cannot be reattached. .NET-only.

**Value:** [BrowserProtocol](/api/E2E.Engine/BrowserProtocol/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L82)

<a id="E2E_Engine_BrowserLease_ReconnectEndpoint_"></a>
<a id="E2E_Engine_BrowserLease_ReconnectEndpoint"></a>

### ReconnectEndpoint


```csharp
public string? ReconnectEndpoint { get; init; }
```


Endpoint to reattach to the same browser after a transport drop, when it differs from [CdpEndpoint](#E2E_Engine_BrowserLease_CdpEndpoint);
only meaningful for a per-attempt lease.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L88)
