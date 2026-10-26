---
title: "WebResponse"
description: "A response CancellationToken) matched."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebResponse"
  hidden: true
---

<a id="E2E_WebResponse"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class WebResponse
```


A response `CancellationToken)` matched.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L469)

## Properties

<a id="E2E_WebResponse_Headers_"></a>
<a id="E2E_WebResponse_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string> Headers { get; }
```


Response headers, with lower-cased names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L492)

<a id="E2E_WebResponse_Status_"></a>
<a id="E2E_WebResponse_Status"></a>

### Status


```csharp
public int Status { get; }
```


HTTP status.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L489)

<a id="E2E_WebResponse_Url_"></a>
<a id="E2E_WebResponse_Url"></a>

### Url


```csharp
public string Url { get; }
```


Response URL.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L486)

## Methods

<a id="E2E_WebResponse_JsonAsync_"></a>
<a id="E2E_WebResponse_JsonAsync__1_System_Threading_CancellationToken_"></a>

### `JsonAsync<T>(CancellationToken)`


```csharp
public Task<T?> JsonAsync<T>(CancellationToken cancellationToken = default)
```


Waits for the body as [TextAsync(CancellationToken)](#E2E_WebResponse_TextAsync_System_Threading_CancellationToken_) does and deserializes it as JSON.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L512)

<a id="E2E_WebResponse_TextAsync_"></a>
<a id="E2E_WebResponse_TextAsync_System_Threading_CancellationToken_"></a>

### TextAsync(CancellationToken)


```csharp
public Task<string> TextAsync(CancellationToken cancellationToken = default)
```


Waits for the body, up to the action timeout, and reads it as text. Fails with
`ACTION_FAILED` when the body could not be read or did not finish in time.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L498)
