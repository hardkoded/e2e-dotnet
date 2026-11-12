---
title: "ISessionApp"
description: "Upstream's SessionApp: the app's session hooks, each failing with UNSUPPORTED_CAPABILITY when the engine lacks it."
editUrl: false
prev: false
next: false
sidebar:
  label: "ISessionApp"
  hidden: true
---

<a id="E2E_Engine_ISessionApp"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface ISessionApp
```


Upstream's `SessionApp`: the app's session hooks, each failing with `UNSUPPORTED_CAPABILITY` when the engine lacks it.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Methods

<a id="E2E_Engine_ISessionApp_BackAsync_"></a>
<a id="E2E_Engine_ISessionApp_BackAsync_E2E_Engine_OperationContext_"></a>

### BackAsync(OperationContext)


```csharp
Task BackAsync(OperationContext operation)
```


Navigates back once.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ISessionApp_OpenAsync_"></a>
<a id="E2E_Engine_ISessionApp_OpenAsync_System_String_E2E_Engine_OperationContext_"></a>

### OpenAsync(string, OperationContext)


```csharp
Task OpenAsync(string url, OperationContext operation)
```


Opens the app at one resolved URL.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ISessionApp_ResetAsync_"></a>
<a id="E2E_Engine_ISessionApp_ResetAsync_E2E_Engine_OperationContext_"></a>

### ResetAsync(OperationContext)


```csharp
Task ResetAsync(OperationContext operation)
```


Clears persisted client state and recreates the context; shows nothing until `OpenAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ISessionApp_ResizeAsync_"></a>
<a id="E2E_Engine_ISessionApp_ResizeAsync_E2E_Reports_ViewportSize_E2E_Engine_OperationContext_"></a>

### ResizeAsync(ViewportSize, OperationContext)


```csharp
Task ResizeAsync(ViewportSize size, OperationContext operation)
```


Sets the viewport size in CSS pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `size` | [ViewportSize](/api/E2E.Reports/ViewportSize/) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ISessionApp_RestartAsync_"></a>
<a id="E2E_Engine_ISessionApp_RestartAsync_E2E_Engine_OperationContext_"></a>

### RestartAsync(OperationContext)


```csharp
Task RestartAsync(OperationContext operation)
```


Recreates the app context without clearing persisted state; shows nothing until `OpenAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
