---
title: "CoreAgent"
description: "An agent over one session, configured in code: ActAsync, AssertAsync, WaitForAsync, and ExtractAsync behave as they do in a test. With a cache, ActAsync repl..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CoreAgent"
  hidden: true
---

<a id="E2E_CoreAgent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CoreAgent
```


An agent over one session, configured in code: `ActAsync`, `AssertAsync`, `WaitForAsync`, and `ExtractAsync` behave as they
do in a test. With a cache, `ActAsync` replays recorded flows, each keyed by the [CacheKey](/api/E2E/CachingActOptions/#E2E_CachingActOptions_CacheKey) the call names.
Upstream's `CachingAgent`, `CoreAgent`, and `UncheckedAgent` (`core/agent.ts`), which TypeScript tells apart by the options
`createAgent` was called with: C# has one type, and refuses at runtime the `ActAsync` call that does not fit the agent.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/CoreAgent.cs)

## Methods

<a id="E2E_CoreAgent_ActAsync_"></a>
<a id="E2E_CoreAgent_ActAsync_System_String_E2E_CachingActOptions_System_Threading_CancellationToken_"></a>

### ActAsync(string, CachingActOptions?, CancellationToken)


```csharp
public Task<ActResult> ActAsync(string instruction, CachingActOptions? options = null, CancellationToken cancellationToken = default)
```


Runs one goal. On an agent with a cache the call names its recording with [CacheKey](/api/E2E/CachingActOptions/#E2E_CachingActOptions_CacheKey), or runs live with
[Cache](/api/E2E/CachingActOptions/#E2E_CachingActOptions_Cache) set to `false`; without one it fails with `INVALID_ARGUMENT`. On an agent without a cache,
naming either fails with `INVALID_ARGUMENT`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instruction` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [CachingActOptions](/api/E2E/CachingActOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ActResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_CoreAgent_AssertAsync_"></a>
<a id="E2E_CoreAgent_AssertAsync_System_String_E2E_AssertOptions_System_Threading_CancellationToken_"></a>

### AssertAsync(string, AssertOptions?, CancellationToken)


```csharp
public Task AssertAsync(string statement, AssertOptions? options = null, CancellationToken cancellationToken = default)
```


Checks that the statement holds on the current screen. See `AssertAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `statement` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [AssertOptions](/api/E2E/AssertOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_CoreAgent_ExtractAsync_"></a>
<a id="E2E_CoreAgent_ExtractAsync__1_System_String_E2E_ExtractOptions_System_Threading_CancellationToken_"></a>

### `ExtractAsync<T>(string, ExtractOptions?, CancellationToken)`


```csharp
public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options = null, CancellationToken cancellationToken = default)
```


Reads data off the current screen. See `ExtractAsync&lt;T&gt;`.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instruction` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ExtractOptions](/api/E2E/ExtractOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_CoreAgent_WaitForAsync_"></a>
<a id="E2E_CoreAgent_WaitForAsync_System_String_E2E_WaitForOptions_System_Threading_CancellationToken_"></a>

### WaitForAsync(string, WaitForOptions?, CancellationToken)


```csharp
public Task WaitForAsync(string statement, WaitForOptions? options = null, CancellationToken cancellationToken = default)
```


Waits until the statement holds on screen. See `WaitForAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `statement` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [WaitForOptions](/api/E2E/WaitForOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
