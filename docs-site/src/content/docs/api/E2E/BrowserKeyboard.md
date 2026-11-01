---
title: "BrowserKeyboard"
description: "Keyboard input for whatever holds focus in the active tab. A call the page does not answer within the action timeout fails with ACTION_MAY_HAVE_COMMITTED: th..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserKeyboard"
  hidden: true
---

<a id="E2E_BrowserKeyboard"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BrowserKeyboard
```


Keyboard input for whatever holds focus in the active tab. A call the page does not
answer within the action timeout fails with `ACTION_MAY_HAVE_COMMITTED`: the input may have reached the page.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L605)

## Methods

<a id="E2E_BrowserKeyboard_DownAsync_"></a>
<a id="E2E_BrowserKeyboard_DownAsync_System_String_System_Threading_CancellationToken_"></a>

### DownAsync(string, CancellationToken)


```csharp
public Task DownAsync(string key, CancellationToken cancellationToken = default)
```


Holds one key until `CancellationToken)`; a repeated down sends a repeat event.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L622)

<a id="E2E_BrowserKeyboard_PressAsync_"></a>
<a id="E2E_BrowserKeyboard_PressAsync_System_String_System_Threading_CancellationToken_"></a>

### PressAsync(string, CancellationToken)


```csharp
public Task PressAsync(string key, CancellationToken cancellationToken = default)
```


Sends one key, such as `Enter` or `Control+A`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L615)

<a id="E2E_BrowserKeyboard_TypeAsync_"></a>
<a id="E2E_BrowserKeyboard_TypeAsync_System_String_System_Threading_CancellationToken_"></a>

### TypeAsync(string, CancellationToken)


```csharp
public Task TypeAsync(string text, CancellationToken cancellationToken = default)
```


Types plain text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L636)

<a id="E2E_BrowserKeyboard_UpAsync_"></a>
<a id="E2E_BrowserKeyboard_UpAsync_System_String_System_Threading_CancellationToken_"></a>

### UpAsync(string, CancellationToken)


```csharp
public Task UpAsync(string key, CancellationToken cancellationToken = default)
```


Releases a held key.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L629)
