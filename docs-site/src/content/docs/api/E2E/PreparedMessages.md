---
title: "PreparedMessages"
description: "What PrepareMessages hands back: the history for the next turn, and optionally a reason to stop. The executor has evidence the loop guards cannot see that th..."
editUrl: false
prev: false
next: false
sidebar:
  label: "PreparedMessages"
  hidden: true
---

<a id="E2E_PreparedMessages"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record PreparedMessages : IEquatable<PreparedMessages>
```


What [PrepareMessages](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_PrepareMessages) hands back: the history for the next turn, and optionally a reason to
stop. The executor has evidence the loop guards cannot see that the step is not progressing. A stop leaves the model only `done`, like a loop guard.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PreparedMessages&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L150)

## Constructors

<a id="E2E_PreparedMessages__ctor_"></a>
<a id="E2E_PreparedMessages__ctor_System_Collections_Generic_IReadOnlyList_E2E_ModelMessage__System_String_"></a>

### `PreparedMessages(IReadOnlyList<ModelMessage>, string?)`


```csharp
public PreparedMessages(IReadOnlyList<ModelMessage> Messages, string? Stop = null)
```


What [PrepareMessages](/api/E2E/ToolLoopExecutorOptions/#E2E_ToolLoopExecutorOptions_PrepareMessages) hands back: the history for the next turn, and optionally a reason to
stop. The executor has evidence the loop guards cannot see that the step is not progressing. A stop leaves the model only `done`, like a loop guard.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Messages` | [IReadOnlyList&lt;ModelMessage&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `Stop` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L150)

## Properties

<a id="E2E_PreparedMessages_Messages_"></a>
<a id="E2E_PreparedMessages_Messages"></a>

### Messages


```csharp
public IReadOnlyList<ModelMessage> Messages { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelMessage&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L150)

<a id="E2E_PreparedMessages_Stop_"></a>
<a id="E2E_PreparedMessages_Stop"></a>

### Stop


```csharp
public string? Stop { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L150)

## Operators

<a id="E2E_PreparedMessages_op_Implicit_"></a>
<a id="E2E_PreparedMessages_op_Implicit_System_Collections_Generic_List_E2E_ModelMessage___E2E_PreparedMessages"></a>

### `implicit operator PreparedMessages(List<ModelMessage>)`


```csharp
public static implicit operator PreparedMessages(List<ModelMessage> messages)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `messages` | [List&lt;ModelMessage&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1) |  |

**Returns:** [PreparedMessages](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L153)
