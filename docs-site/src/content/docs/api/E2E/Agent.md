---
title: "Agent"
description: "One goal (CancellationToken)) or a check against the current screen."
editUrl: false
prev: false
next: false
sidebar:
  label: "Agent"
  hidden: true
---

<a id="E2E_Agent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Agent
```


One goal (`CancellationToken)`) or a check against the current screen.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L19)

## Methods

<a id="E2E_Agent_ActAsync_"></a>
<a id="E2E_Agent_ActAsync_System_String_E2E_ActOptions_System_Threading_CancellationToken_"></a>

### ActAsync(string, ActOptions?, CancellationToken)


```csharp
public Task<ActResult> ActAsync(string instruction, ActOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instruction` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ActOptions](/api/E2E/ActOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ActResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L73)

<a id="E2E_Agent_AssertAsync_"></a>
<a id="E2E_Agent_AssertAsync_System_String_E2E_AssertOptions_System_Threading_CancellationToken_"></a>

### AssertAsync(string, AssertOptions?, CancellationToken)


```csharp
public Task AssertAsync(string statement, AssertOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `statement` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [AssertOptions](/api/E2E/AssertOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L76)

<a id="E2E_Agent_ExtractAsync_"></a>
<a id="E2E_Agent_ExtractAsync__1_System_String_E2E_ExtractOptions_System_Threading_CancellationToken_"></a>

### `ExtractAsync<T>(string, ExtractOptions?, CancellationToken)`


```csharp
public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options = null, CancellationToken cancellationToken = default)
```


Reads data of type &lt;code class="typeparamref"&gt;T&lt;/code&gt; from the current screen. &lt;code class="typeparamref"&gt;T&lt;/code&gt; is the
schema: its JSON schema is sent to the judge, and the answer must deserialize into it with required
members and nullable annotations respected. An answer that does not gets one repair round, then
`MODEL_OUTPUT_INVALID`.

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L189)

<a id="E2E_Agent_WaitForAsync_"></a>
<a id="E2E_Agent_WaitForAsync_System_String_E2E_WaitForOptions_System_Threading_CancellationToken_"></a>

### WaitForAsync(string, WaitForOptions?, CancellationToken)


```csharp
public Task WaitForAsync(string statement, WaitForOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `statement` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [WaitForOptions](/api/E2E/WaitForOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L90)
