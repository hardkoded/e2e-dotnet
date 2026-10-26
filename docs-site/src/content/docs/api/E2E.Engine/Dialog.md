---
title: "Dialog"
description: "A native dialog as a test's handler sees it (upstream's Dialog)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Dialog"
  hidden: true
---

<a id="E2E_Engine_Dialog"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class Dialog
```


A native dialog as a test's handler sees it (upstream's `Dialog`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L26)

## Properties

<a id="E2E_Engine_Dialog_Message_"></a>
<a id="E2E_Engine_Dialog_Message"></a>

### Message


```csharp
public string Message { get; }
```


The dialog's text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L43)

<a id="E2E_Engine_Dialog_Type_"></a>
<a id="E2E_Engine_Dialog_Type"></a>

### Type


```csharp
public DialogType Type { get; }
```


The native dialog kind, independent of its message.

**Value:** [DialogType](/api/E2E.Engine/DialogType/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L40)

## Methods

<a id="E2E_Engine_Dialog_AcceptAsync_"></a>
<a id="E2E_Engine_Dialog_AcceptAsync_System_String_"></a>

### AcceptAsync(string?)


```csharp
public Task AcceptAsync(string? text = null)
```


Accepts the dialog once. &lt;code class="paramref"&gt;text&lt;/code&gt; answers a prompt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L46)

<a id="E2E_Engine_Dialog_DismissAsync_"></a>
<a id="E2E_Engine_Dialog_DismissAsync"></a>

### DismissAsync()


```csharp
public Task DismissAsync()
```


Dismisses the dialog once.

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Dialogs.cs#L49)
