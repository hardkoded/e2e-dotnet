---
title: "CdpTarget"
description: "A CDP connection endpoint and its handshake headers, resolved together (upstream CdpTarget). A bare endpoint converts implicitly."
editUrl: false
prev: false
next: false
sidebar:
  label: "CdpTarget"
  hidden: true
---

<a id="E2E_Engine_CdpTarget"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record CdpTarget : IEquatable<CdpTarget>
```


A CDP connection endpoint and its handshake headers, resolved together (upstream `CdpTarget`). A bare endpoint converts implicitly.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;CdpTarget&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserConnection.cs)

## Properties

<a id="E2E_Engine_CdpTarget_Endpoint_"></a>
<a id="E2E_Engine_CdpTarget_Endpoint"></a>

### Endpoint


```csharp
public required string Endpoint { get; init; }
```


The `ws://`, `wss://`, or `http://` DevTools URL.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_CdpTarget_Headers_"></a>
<a id="E2E_Engine_CdpTarget_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Headers sent with the CDP handshake only, never with the page's own requests.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

## Operators

<a id="E2E_Engine_CdpTarget_op_Implicit_"></a>
<a id="E2E_Engine_CdpTarget_op_Implicit_System_String__E2E_Engine_CdpTarget"></a>

### implicit operator CdpTarget(string)


```csharp
public static implicit operator CdpTarget(string endpoint)
```


Wraps an endpoint that needs no handshake headers.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `endpoint` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [CdpTarget](./)
