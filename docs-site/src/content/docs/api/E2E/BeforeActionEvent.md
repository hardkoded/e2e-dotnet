---
title: "BeforeActionEvent"
description: "One action of an agent.act step about to run, as OnBeforeAction receives it."
editUrl: false
prev: false
next: false
sidebar:
  label: "BeforeActionEvent"
  hidden: true
---

<a id="E2E_BeforeActionEvent"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record BeforeActionEvent : IEquatable<BeforeActionEvent>
```


One action of an `agent.act` step about to run, as [OnBeforeAction](/api/E2E/ActOptions/#E2E_ActOptions_OnBeforeAction) receives it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BeforeActionEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)

## Constructors

<a id="E2E_BeforeActionEvent__ctor_"></a>
<a id="E2E_BeforeActionEvent__ctor_System_Int32_E2E_ActionSource_"></a>

### BeforeActionEvent(int, ActionSource)


```csharp
public BeforeActionEvent(int Index, ActionSource Source)
```


One action of an `agent.act` step about to run, as [OnBeforeAction](/api/E2E/ActOptions/#E2E_ActOptions_OnBeforeAction) receives it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The same index the action's [Index](/api/E2E/ActionEvent/#E2E_ActionEvent_Index) carries. |
| `Source` | [ActionSource](/api/E2E/ActionSource/) | [Model](/api/E2E/ActionSource/#E2E_ActionSource_Model) when the executor chose the action, [Replay](/api/E2E/ActionSource/#E2E_ActionSource_Replay) when the cache replays it. |

## Properties

<a id="E2E_BeforeActionEvent_Index_"></a>
<a id="E2E_BeforeActionEvent_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


The same index the action's [Index](/api/E2E/ActionEvent/#E2E_ActionEvent_Index) carries.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_BeforeActionEvent_Source_"></a>
<a id="E2E_BeforeActionEvent_Source"></a>

### Source


```csharp
public ActionSource Source { get; init; }
```


[Model](/api/E2E/ActionSource/#E2E_ActionSource_Model) when the executor chose the action, [Replay](/api/E2E/ActionSource/#E2E_ActionSource_Replay) when the cache replays it.

**Value:** [ActionSource](/api/E2E/ActionSource/)
