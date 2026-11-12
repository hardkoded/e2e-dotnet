---
title: "ISessionKeyboard"
description: "Upstream's SessionKeyboard: typing, pressing, and dismissing for whatever holds focus."
editUrl: false
prev: false
next: false
sidebar:
  label: "ISessionKeyboard"
  hidden: true
---

<a id="E2E_Engine_ISessionKeyboard"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface ISessionKeyboard
```


Upstream's `SessionKeyboard`: typing, pressing, and dismissing for whatever holds focus.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Methods

<a id="E2E_Engine_ISessionKeyboard_DismissAsync_"></a>
<a id="E2E_Engine_ISessionKeyboard_DismissAsync_E2E_Engine_OperationContext_"></a>

### DismissAsync(OperationContext)


```csharp
Task DismissAsync(OperationContext operation)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ISessionKeyboard_PressAsync_"></a>
<a id="E2E_Engine_ISessionKeyboard_PressAsync_System_String_E2E_Engine_OperationContext_"></a>

### PressAsync(string, OperationContext)


```csharp
Task PressAsync(string key, OperationContext operation)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ISessionKeyboard_TypeAsync_"></a>
<a id="E2E_Engine_ISessionKeyboard_TypeAsync_System_String_System_Boolean_E2E_Engine_OperationContext_"></a>

### TypeAsync(string, bool, OperationContext)


```csharp
Task TypeAsync(string text, bool replace, OperationContext operation)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `replace` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
