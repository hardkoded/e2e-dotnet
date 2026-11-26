---
title: "ConnectionOptions"
description: "How a connection handles what arrives on it. Upstream's ConnectionOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "ConnectionOptions"
  hidden: true
---

<a id="E2E_Engine_Protocol_ConnectionOptions"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record ConnectionOptions : IEquatable<ConnectionOptions>
```


How a connection handles what arrives on it. Upstream's `ConnectionOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ConnectionOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Connection.cs)

## Properties

<a id="E2E_Engine_Protocol_ConnectionOptions_EncodeError_"></a>
<a id="E2E_Engine_Protocol_ConnectionOptions_EncodeError"></a>

### EncodeError


```csharp
public Func<Exception, RpcErrorObject>? EncodeError { get; init; }
```


Encodes a handler failure; defaults to an internal error carrying the message.

**Value:** [Func&lt;Exception, RpcErrorObject&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_Protocol_ConnectionOptions_Notifications_"></a>
<a id="E2E_Engine_Protocol_ConnectionOptions_Notifications"></a>

### Notifications


```csharp
public IReadOnlyDictionary<string, Action<JsonNode?>>? Notifications { get; init; }
```


Handles one incoming notification by method.

**Value:** [IReadOnlyDictionary&lt;string, Action&lt;JsonNode&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_Protocol_ConnectionOptions_OnClose_"></a>
<a id="E2E_Engine_Protocol_ConnectionOptions_OnClose"></a>

### OnClose


```csharp
public Action<string>? OnClose { get; init; }
```


Called once when the input ends or fails, after every pending request was rejected.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_Protocol_ConnectionOptions_OnNotificationError_"></a>
<a id="E2E_Engine_Protocol_ConnectionOptions_OnNotificationError"></a>

### OnNotificationError


```csharp
public Action<string, Exception>? OnNotificationError { get; init; }
```


Called with what a notification handler threw: a notification has no reply to carry it.

**Value:** [Action&lt;string, Exception&gt;](https://learn.microsoft.com/dotnet/api/system.action-2)

<a id="E2E_Engine_Protocol_ConnectionOptions_Requests_"></a>
<a id="E2E_Engine_Protocol_ConnectionOptions_Requests"></a>

### Requests


```csharp
public IReadOnlyDictionary<string, Func<JsonNode?, Task<JsonNode?>>>? Requests { get; init; }
```


Answers one incoming request by method; what a handler throws becomes the error response.

**Value:** [IReadOnlyDictionary&lt;string, Func&lt;JsonNode, Task&lt;JsonNode&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
