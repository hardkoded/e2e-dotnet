---
title: "NodeStates"
description: "Boolean states a platform can report for one node."
editUrl: false
prev: false
next: false
sidebar:
  label: "NodeStates"
  hidden: true
---

<a id="E2E_Engine_NodeStates"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class NodeStates
```


Boolean states a platform can report for one node.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs)

## Properties

<a id="E2E_Engine_NodeStates_AriaHidden_"></a>
<a id="E2E_Engine_NodeStates_AriaHidden"></a>

### AriaHidden


```csharp
public bool AriaHidden { get; init; }
```


The node, or an ancestor, is `aria-hidden`. It may still paint, so it can be visible, but role queries and the agent skip it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Checked_"></a>
<a id="E2E_Engine_NodeStates_Checked"></a>

### Checked


```csharp
public bool Checked { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_CheckedReported_"></a>
<a id="E2E_Engine_NodeStates_CheckedReported"></a>

### CheckedReported


```csharp
public bool CheckedReported { get; init; }
```


The platform reported a checked state for the node, true or false. Setting [Checked](#E2E_Engine_NodeStates_Checked) reports it; a node with none
(a button, a heading) leaves it false, and `IsChecked` fails on it unless its role carries a checked state. List it after
[Checked](#E2E_Engine_NodeStates_Checked) in an initializer to override that (.NET-only; upstream reads the state as nullable).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Disabled_"></a>
<a id="E2E_Engine_NodeStates_Disabled"></a>

### Disabled


```csharp
public bool Disabled { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_EmptyBox_"></a>
<a id="E2E_Engine_NodeStates_EmptyBox"></a>

### EmptyBox


```csharp
public bool EmptyBox { get; init; }
```


The node renders a box with no width or no height. It is not visible, though role queries and the agent still read it (.NET-only; upstream puts this in its `hidden` state). Web only.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Expanded_"></a>
<a id="E2E_Engine_NodeStates_Expanded"></a>

### Expanded


```csharp
public bool Expanded { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_ExpandedAbsent_"></a>
<a id="E2E_Engine_NodeStates_ExpandedAbsent"></a>

### ExpandedAbsent


```csharp
public bool ExpandedAbsent { get; init; }
```


The node has no `aria-expanded` of true or false. A role query for `Expanded = false` skips it. Web only (.NET-only; upstream reads the state as nullable).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Focused_"></a>
<a id="E2E_Engine_NodeStates_Focused"></a>

### Focused


```csharp
public bool Focused { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Hidden_"></a>
<a id="E2E_Engine_NodeStates_Hidden"></a>

### Hidden


```csharp
public bool Hidden { get; init; }
```


The node does not render: `display: none`, `visibility`, skipped content, a closed `details`. This is what `isVisible` reads.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Mixed_"></a>
<a id="E2E_Engine_NodeStates_Mixed"></a>

### Mixed


```csharp
public bool Mixed { get; init; }
```


The node is a checkbox or a button in the mixed state. A role query for `Checked` or `Pressed` of either value skips it. Web only (.NET-only; upstream reads the state as nullable).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Pressed_"></a>
<a id="E2E_Engine_NodeStates_Pressed"></a>

### Pressed


```csharp
public bool Pressed { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Secure_"></a>
<a id="E2E_Engine_NodeStates_Secure"></a>

### Secure


```csharp
public bool Secure { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_NodeStates_Selected_"></a>
<a id="E2E_Engine_NodeStates_Selected"></a>

### Selected


```csharp
public bool Selected { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
