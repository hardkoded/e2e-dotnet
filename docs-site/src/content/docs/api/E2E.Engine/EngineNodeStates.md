---
title: "EngineNodeStates"
description: "Upstream's SemanticNode.states: the boolean states the platform reports. A null flag is absent: the platform does not say. Readonly is present only on a node..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineNodeStates"
  hidden: true
---

<a id="E2E_Engine_EngineNodeStates"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineNodeStates : IEquatable<EngineNodeStates>
```


Upstream's `SemanticNode.states`: the boolean states the platform reports. A null flag is absent: the platform does not say.
[Readonly](#E2E_Engine_EngineNodeStates_Readonly) is present only on a node that has a read-only state. [Empty](#E2E_Engine_EngineNodeStates_Empty) is whether the node holds nothing, reported on
the nodes `Locate` returns, never in an observation, and never on a secure field.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineNodeStates&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_EngineNodeStates__ctor_"></a>
<a id="E2E_Engine_EngineNodeStates__ctor_System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__System_Nullable_System_Boolean__"></a>

### EngineNodeStates(bool?, bool?, bool?, bool?, bool?, bool?, bool?, bool?, bool?, bool?)


```csharp
public EngineNodeStates(bool? Secure = null, bool? Hidden = null, bool? Checked = null, bool? Disabled = null, bool? Selected = null, bool? Expanded = null, bool? Pressed = null, bool? Focused = null, bool? Readonly = null, bool? Empty = null)
```


Upstream's `SemanticNode.states`: the boolean states the platform reports. A null flag is absent: the platform does not say.
[Readonly](#E2E_Engine_EngineNodeStates_Readonly) is present only on a node that has a read-only state. [Empty](#E2E_Engine_EngineNodeStates_Empty) is whether the node holds nothing, reported on
the nodes `Locate` returns, never in an observation, and never on a secure field.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Secure` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Hidden` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Checked` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Disabled` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Selected` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Expanded` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Pressed` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Focused` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Readonly` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Empty` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

## Properties

<a id="E2E_Engine_EngineNodeStates_Checked_"></a>
<a id="E2E_Engine_EngineNodeStates_Checked"></a>

### Checked


```csharp
public bool? Checked { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Disabled_"></a>
<a id="E2E_Engine_EngineNodeStates_Disabled"></a>

### Disabled


```csharp
public bool? Disabled { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Empty_"></a>
<a id="E2E_Engine_EngineNodeStates_Empty"></a>

### Empty


```csharp
public bool? Empty { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Expanded_"></a>
<a id="E2E_Engine_EngineNodeStates_Expanded"></a>

### Expanded


```csharp
public bool? Expanded { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Focused_"></a>
<a id="E2E_Engine_EngineNodeStates_Focused"></a>

### Focused


```csharp
public bool? Focused { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Hidden_"></a>
<a id="E2E_Engine_EngineNodeStates_Hidden"></a>

### Hidden


```csharp
public bool? Hidden { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Pressed_"></a>
<a id="E2E_Engine_EngineNodeStates_Pressed"></a>

### Pressed


```csharp
public bool? Pressed { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Readonly_"></a>
<a id="E2E_Engine_EngineNodeStates_Readonly"></a>

### Readonly


```csharp
public bool? Readonly { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Secure_"></a>
<a id="E2E_Engine_EngineNodeStates_Secure"></a>

### Secure


```csharp
public bool? Secure { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Selected_"></a>
<a id="E2E_Engine_EngineNodeStates_Selected"></a>

### Selected


```csharp
public bool? Selected { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)
