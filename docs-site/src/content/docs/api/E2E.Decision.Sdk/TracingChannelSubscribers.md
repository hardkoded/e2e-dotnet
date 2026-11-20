---
title: "TracingChannelSubscribers"
description: "The subscribers of a TracingChannel (TracingChannelSubscribers). Each one is optional."
editUrl: false
prev: false
next: false
sidebar:
  label: "TracingChannelSubscribers"
  hidden: true
---

<a id="E2E_Decision_Sdk_TracingChannelSubscribers"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class TracingChannelSubscribers
```


The subscribers of a [TracingChannel](/api/E2E.Decision.Sdk/TracingChannel/) (`TracingChannelSubscribers`). Each one is optional.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DiagnosticsChannel.cs)

## Properties

<a id="E2E_Decision_Sdk_TracingChannelSubscribers_AsyncEnd_"></a>
<a id="E2E_Decision_Sdk_TracingChannelSubscribers_AsyncEnd"></a>

### AsyncEnd


```csharp
public Action<object>? AsyncEnd { get; init; }
```


Called when the operation's task completes.

**Value:** [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Decision_Sdk_TracingChannelSubscribers_AsyncStart_"></a>
<a id="E2E_Decision_Sdk_TracingChannelSubscribers_AsyncStart"></a>

### AsyncStart


```csharp
public Action<object>? AsyncStart { get; init; }
```


Called when the operation's task completes, before [AsyncEnd](#E2E_Decision_Sdk_TracingChannelSubscribers_AsyncEnd).

**Value:** [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Decision_Sdk_TracingChannelSubscribers_End_"></a>
<a id="E2E_Decision_Sdk_TracingChannelSubscribers_End"></a>

### End


```csharp
public Action<object>? End { get; init; }
```


Called when the operation's synchronous part returns.

**Value:** [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Decision_Sdk_TracingChannelSubscribers_Error_"></a>
<a id="E2E_Decision_Sdk_TracingChannelSubscribers_Error"></a>

### Error


```csharp
public Action<object>? Error { get; init; }
```


Called when the operation fails.

**Value:** [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Decision_Sdk_TracingChannelSubscribers_Start_"></a>
<a id="E2E_Decision_Sdk_TracingChannelSubscribers_Start"></a>

### Start


```csharp
public Action<object>? Start { get; init; }
```


Called when the operation starts.

**Value:** [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)
