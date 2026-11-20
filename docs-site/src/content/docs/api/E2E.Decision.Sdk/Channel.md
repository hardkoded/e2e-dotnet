---
title: "Channel"
description: "A named channel that delivers each published message to its subscribers (diagnosticsChannel.channel)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Channel"
  hidden: true
---

<a id="E2E_Decision_Sdk_Channel"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class Channel
```


A named channel that delivers each published message to its subscribers (`diagnosticsChannel.channel`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DiagnosticsChannel.cs)

## Properties

<a id="E2E_Decision_Sdk_Channel_HasSubscribers_"></a>
<a id="E2E_Decision_Sdk_Channel_HasSubscribers"></a>

### HasSubscribers


```csharp
public bool HasSubscribers { get; }
```


Whether a subscriber or a bound store would see a message.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_Channel_Name_"></a>
<a id="E2E_Decision_Sdk_Channel_Name"></a>

### Name


```csharp
public string Name { get; }
```


The channel's name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Sdk_Channel_BindStore_"></a>
<a id="E2E_Decision_Sdk_Channel_BindStore__1_System_Threading_AsyncLocal___0__System_Func_System_Object___0__"></a>

### `BindStore<T>(AsyncLocal<T?>, Func<object, T?>)`


```csharp
public void BindStore<T>(AsyncLocal<T?> store, Func<object, T?> transform)
```


`channel.bindStore`: while `RunStores&lt;T&gt;` runs, &lt;code class="paramref"&gt;store&lt;/code&gt; holds what &lt;code class="paramref"&gt;transform&lt;/code&gt; returns
for the message.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `store` | [AsyncLocal&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1) |  |
| `transform` | [Func&lt;object, T&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

<a id="E2E_Decision_Sdk_Channel_Publish_"></a>
<a id="E2E_Decision_Sdk_Channel_Publish_System_Object_"></a>

### Publish(object)


```csharp
public void Publish(object message)
```


`channel.publish`. A subscriber that throws does not affect the publisher or the other subscribers, as in Node, where the error
is reported out of band.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

<a id="E2E_Decision_Sdk_Channel_RunStores_"></a>
<a id="E2E_Decision_Sdk_Channel_RunStores__1_System_Object_System_Func___0__"></a>

### `RunStores<T>(object, Func<T>)`


```csharp
public T RunStores<T>(object message, Func<T> execute)
```


`channel.runStores`: binds the stores to the message, publishes it, and runs &lt;code class="paramref"&gt;execute&lt;/code&gt; inside them.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
| `execute` | [Func&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |

**Returns:** `T`

<a id="E2E_Decision_Sdk_Channel_Subscribe_"></a>
<a id="E2E_Decision_Sdk_Channel_Subscribe_System_Action_System_Object__"></a>

### `Subscribe(Action<object>)`


```csharp
public void Subscribe(Action<object> subscriber)
```


`channel.subscribe`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `subscriber` | [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1) |  |

<a id="E2E_Decision_Sdk_Channel_UnbindStore_"></a>
<a id="E2E_Decision_Sdk_Channel_UnbindStore__1_System_Threading_AsyncLocal___0__"></a>

### `UnbindStore<T>(AsyncLocal<T?>)`


```csharp
public bool UnbindStore<T>(AsyncLocal<T?> store)
```


`channel.unbindStore`. Returns whether the store was bound.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `store` | [AsyncLocal&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_Channel_Unsubscribe_"></a>
<a id="E2E_Decision_Sdk_Channel_Unsubscribe_System_Action_System_Object__"></a>

### `Unsubscribe(Action<object>)`


```csharp
public bool Unsubscribe(Action<object> subscriber)
```


`channel.unsubscribe`. Returns whether the subscriber was subscribed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `subscriber` | [Action&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.action-1) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
