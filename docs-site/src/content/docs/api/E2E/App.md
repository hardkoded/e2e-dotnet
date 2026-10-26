---
title: "App"
description: "Opens and resets the app under test. A relative URL is resolved against BaseUrl; no URL opens the base URL."
editUrl: false
prev: false
next: false
sidebar:
  label: "App"
  hidden: true
---

<a id="E2E_App"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class App
```


Opens and resets the app under test. A relative URL is resolved against
[BaseUrl](#E2E_App_BaseUrl); no URL opens the base URL.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L14)

## Properties

<a id="E2E_App_BaseUrl_"></a>
<a id="E2E_App_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; }
```


The target base URL relative URLs resolve against, or null when the test declares none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L35)

## Methods

<a id="E2E_App_BackAsync_"></a>
<a id="E2E_App_BackAsync_System_Threading_CancellationToken_"></a>

### BackAsync(CancellationToken)


```csharp
public Task BackAsync(CancellationToken cancellationToken = default)
```


Goes back one entry in the history. With no earlier entry it does nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L73)

<a id="E2E_App_ClearStateAsync_"></a>
<a id="E2E_App_ClearStateAsync_System_Threading_CancellationToken_"></a>

### ClearStateAsync(CancellationToken)


```csharp
public Task ClearStateAsync(CancellationToken cancellationToken = default)
```


Discards cookies, storage, and history, and reopens the app at [BaseUrl](#E2E_App_BaseUrl).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L85)

<a id="E2E_App_OpenAsync_"></a>
<a id="E2E_App_OpenAsync_System_String_System_Threading_CancellationToken_"></a>

### OpenAsync(string?, CancellationToken)


```csharp
public Task OpenAsync(string? url = null, CancellationToken cancellationToken = default)
```


Opens a path or URL, resolved against the base URL. No URL opens the base URL itself.
Throws `APP_URL_REQUIRED` for a relative URL when the test declares no base URL, and `POLICY_DENIED`
for a malformed URL or a scheme other than `http:` or `https:` (the exact `about:blank` is admitted).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L42)

<a id="E2E_App_RestartAsync_"></a>
<a id="E2E_App_RestartAsync_System_Threading_CancellationToken_"></a>

### RestartAsync(CancellationToken)


```csharp
public Task RestartAsync(CancellationToken cancellationToken = default)
```


Closes the current document and reopens the app at [BaseUrl](#E2E_App_BaseUrl). Cookies and storage are kept.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Fixtures.cs#L79)
