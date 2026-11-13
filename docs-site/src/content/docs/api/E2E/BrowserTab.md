---
title: "BrowserTab"
description: "One tab of the browser the attempt drives: the attempt's own, one the app opened, or one NewTabAsync(CancellationToken) opened. The same tab is the same obje..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserTab"
  hidden: true
---

<a id="E2E_BrowserTab"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BrowserTab
```


One tab of the browser the attempt drives: the attempt's own, one the app opened, or one [NewTabAsync(CancellationToken)](/api/E2E/Browser/#E2E_Browser_NewTabAsync_System_Threading_CancellationToken_) opened. The same tab
is the same object for the whole attempt.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs)

## Properties

<a id="E2E_BrowserTab_IsActive_"></a>
<a id="E2E_BrowserTab_IsActive"></a>

### IsActive


```csharp
public bool IsActive { get; }
```


Whether this is the active tab, the one the screen, `Expect`, and the browser's page methods act on.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_BrowserTab_IsClosed_"></a>
<a id="E2E_BrowserTab_IsClosed"></a>

### IsClosed


```csharp
public bool IsClosed { get; }
```


Whether the tab has closed.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_BrowserTab_ActivateAsync_"></a>
<a id="E2E_BrowserTab_ActivateAsync_System_Threading_CancellationToken_"></a>

### ActivateAsync(CancellationToken)


```csharp
public Task ActivateAsync(CancellationToken cancellationToken = default)
```


Makes this the active tab. Fails with `ACTION_FAILED` when it has closed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserTab_CloseAsync_"></a>
<a id="E2E_BrowserTab_CloseAsync_System_Threading_CancellationToken_"></a>

### CloseAsync(CancellationToken)


```csharp
public Task CloseAsync(CancellationToken cancellationToken = default)
```


Closes the tab. When it was the active tab, the tab that opened it becomes active, else the open tab that was active most recently.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_BrowserTab_TitleAsync_"></a>
<a id="E2E_BrowserTab_TitleAsync_System_Threading_CancellationToken_"></a>

### TitleAsync(CancellationToken)


```csharp
public Task<string> TitleAsync(CancellationToken cancellationToken = default)
```


Returns the tab's current title.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_BrowserTab_UrlAsync_"></a>
<a id="E2E_BrowserTab_UrlAsync_System_Threading_CancellationToken_"></a>

### UrlAsync(CancellationToken)


```csharp
public Task<string> UrlAsync(CancellationToken cancellationToken = default)
```


Returns the tab's current URL.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
