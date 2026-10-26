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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L18)

## Properties

<a id="E2E_Engine_NodeStates_AriaHidden_"></a>
<a id="E2E_Engine_NodeStates_AriaHidden"></a>

### AriaHidden


```csharp
public bool AriaHidden { get; init; }
```


The node, or an ancestor, is `aria-hidden`. It may still paint, so it can be visible, but role queries and the agent skip it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L32)

<a id="E2E_Engine_NodeStates_Checked_"></a>
<a id="E2E_Engine_NodeStates_Checked"></a>

### Checked


```csharp
public bool Checked { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L20)

<a id="E2E_Engine_NodeStates_Disabled_"></a>
<a id="E2E_Engine_NodeStates_Disabled"></a>

### Disabled


```csharp
public bool Disabled { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L22)

<a id="E2E_Engine_NodeStates_Expanded_"></a>
<a id="E2E_Engine_NodeStates_Expanded"></a>

### Expanded


```csharp
public bool Expanded { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L24)

<a id="E2E_Engine_NodeStates_Focused_"></a>
<a id="E2E_Engine_NodeStates_Focused"></a>

### Focused


```csharp
public bool Focused { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L26)

<a id="E2E_Engine_NodeStates_Hidden_"></a>
<a id="E2E_Engine_NodeStates_Hidden"></a>

### Hidden


```csharp
public bool Hidden { get; init; }
```


The node does not render: `display: none`, `visibility`, skipped content, a closed `details`. This is what `isVisible` reads.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L29)

<a id="E2E_Engine_NodeStates_Pressed_"></a>
<a id="E2E_Engine_NodeStates_Pressed"></a>

### Pressed


```csharp
public bool Pressed { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L38)

<a id="E2E_Engine_NodeStates_Secure_"></a>
<a id="E2E_Engine_NodeStates_Secure"></a>

### Secure


```csharp
public bool Secure { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L34)

<a id="E2E_Engine_NodeStates_Selected_"></a>
<a id="E2E_Engine_NodeStates_Selected"></a>

### Selected


```csharp
public bool Selected { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L36)
