---
title: "ActionEvent"
description: "One completed action of an agent.act step, as OnAction receives it."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActionEvent"
  hidden: true
---

<a id="E2E_ActionEvent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ActionEvent : IEquatable<ActionEvent>
```


One completed action of an `agent.act` step, as [OnAction](/api/E2E/ActOptions/#E2E_ActOptions_OnAction) receives it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1014)

## Constructors

<a id="E2E_ActionEvent__ctor_"></a>
<a id="E2E_ActionEvent__ctor_System_Int32_System_String_System_String_E2E_ActionSource_"></a>

### ActionEvent(int, string, string, ActionSource)


```csharp
public ActionEvent(int Index, string Action, string Summary, ActionSource Source)
```


One completed action of an `agent.act` step, as [OnAction](/api/E2E/ActOptions/#E2E_ActOptions_OnAction) receives it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | 1-based position in the step's action budget; a failed attempt uses a number too. |
| `Action` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The committed action, such as `tap` or `navigate`; a mutating project tool reads `tool:name`. |
| `Summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the action did, secrets redacted. |
| `Source` | [ActionSource](/api/E2E/ActionSource/) | Who chose the action. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1014)

## Properties

<a id="E2E_ActionEvent_Action_"></a>
<a id="E2E_ActionEvent_Action"></a>

### Action


```csharp
public string Action { get; init; }
```


The committed action, such as `tap` or `navigate`; a mutating project tool reads `tool:name`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1014)

<a id="E2E_ActionEvent_Index_"></a>
<a id="E2E_ActionEvent_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


1-based position in the step's action budget; a failed attempt uses a number too.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1014)

<a id="E2E_ActionEvent_Source_"></a>
<a id="E2E_ActionEvent_Source"></a>

### Source


```csharp
public ActionSource Source { get; init; }
```


Who chose the action.

**Value:** [ActionSource](/api/E2E/ActionSource/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1014)

<a id="E2E_ActionEvent_Summary_"></a>
<a id="E2E_ActionEvent_Summary"></a>

### Summary


```csharp
public string Summary { get; init; }
```


What the action did, secrets redacted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1014)
