---
title: "BrowserMouse"
description: "Pointer input for the active tab, in CSS pixels. A call the page does not answer within the action timeout fails with ACTIONMAYHAVECOMMITTED: the input may h..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserMouse"
  hidden: true
---

<a id="E2E_BrowserMouse"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BrowserMouse
```


Pointer input for the active tab, in CSS pixels. A call the page does not answer
within the action timeout fails with `ACTION_MAY_HAVE_COMMITTED`: the input may have reached the page.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L748)

## Methods

<a id="E2E_BrowserMouse_DownAsync_"></a>
<a id="E2E_BrowserMouse_DownAsync_System_Threading_CancellationToken_"></a>

### DownAsync(CancellationToken)


```csharp
public Task DownAsync(CancellationToken cancellationToken = default)
```


Presses the primary button.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L764)

<a id="E2E_BrowserMouse_MoveAsync_"></a>
<a id="E2E_BrowserMouse_MoveAsync_System_Single_System_Single_System_Threading_CancellationToken_"></a>

### MoveAsync(float, float, CancellationToken)


```csharp
public Task MoveAsync(float x, float y, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `x` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `y` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L757)

<a id="E2E_BrowserMouse_UpAsync_"></a>
<a id="E2E_BrowserMouse_UpAsync_System_Threading_CancellationToken_"></a>

### UpAsync(CancellationToken)


```csharp
public Task UpAsync(CancellationToken cancellationToken = default)
```


Releases the primary button.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L768)

<a id="E2E_BrowserMouse_WheelAsync_"></a>
<a id="E2E_BrowserMouse_WheelAsync_System_Single_System_Single_System_Threading_CancellationToken_"></a>

### WheelAsync(float, float, CancellationToken)


```csharp
public Task WheelAsync(float deltaX, float deltaY, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `deltaX` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `deltaY` | [float](https://learn.microsoft.com/dotnet/api/system.single) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L760)
