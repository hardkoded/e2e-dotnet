---
title: "DialogHandler"
description: "What answers a native dialog (upstream's DialogHandler): Accept, Dismiss, or a function that must call AcceptAsync(string) or DismissAsync() before it returns."
editUrl: false
prev: false
next: false
sidebar:
  label: "DialogHandler"
  hidden: true
---

<a id="E2E_Engine_DialogHandler"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class DialogHandler
```


What answers a native dialog (upstream's `DialogHandler`): [Accept](#E2E_Engine_DialogHandler_Accept), [Dismiss](#E2E_Engine_DialogHandler_Dismiss), or a function
that must call [AcceptAsync(string)](/api/E2E.Engine/Dialog/#E2E_Engine_Dialog_AcceptAsync_System_String_) or [DismissAsync()](/api/E2E.Engine/Dialog/#E2E_Engine_Dialog_DismissAsync) before it returns.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L56)

## Properties

<a id="E2E_Engine_DialogHandler_Accept_"></a>
<a id="E2E_Engine_DialogHandler_Accept"></a>

### Accept


```csharp
public static DialogHandler Accept { get; }
```


Accepts every dialog.

**Value:** [DialogHandler](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L65)

<a id="E2E_Engine_DialogHandler_Dismiss_"></a>
<a id="E2E_Engine_DialogHandler_Dismiss"></a>

### Dismiss


```csharp
public static DialogHandler Dismiss { get; }
```


Dismisses every dialog.

**Value:** [DialogHandler](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L68)

## Operators

<a id="E2E_Engine_DialogHandler_op_Implicit_"></a>
<a id="E2E_Engine_DialogHandler_op_Implicit_System_Func_E2E_Engine_Dialog_System_Threading_Tasks_Task___E2E_Engine_DialogHandler"></a>

### `implicit operator DialogHandler(Func<Dialog, Task>)`


```csharp
public static implicit operator DialogHandler(Func<Dialog, Task> handler)
```


A handler that runs &lt;code class="paramref"&gt;handler&lt;/code&gt; for each dialog.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `handler` | [Func&lt;Dialog, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [DialogHandler](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L75)
