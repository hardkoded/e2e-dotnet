---
title: "SemanticNode"
description: "One node of an observation tree. Ref is valid only for the observation it came from."
editUrl: false
prev: false
next: false
sidebar:
  label: "SemanticNode"
  hidden: true
---

<a id="E2E_Engine_SemanticNode"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class SemanticNode
```


One node of an observation tree. [Ref](#E2E_Engine_SemanticNode_Ref) is valid only for the observation it came from.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L45)

## Properties

<a id="E2E_Engine_SemanticNode_Attributes_"></a>
<a id="E2E_Engine_SemanticNode_Attributes"></a>

### Attributes


```csharp
public IReadOnlyDictionary<string, string> Attributes { get; init; }
```


Platform attributes by name, what `getAttribute` and `toHaveAttribute` read.
A secure field never reports its value.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L90)

<a id="E2E_Engine_SemanticNode_Children_"></a>
<a id="E2E_Engine_SemanticNode_Children"></a>

### Children


```csharp
public IReadOnlyList<SemanticNode> Children { get; init; }
```


**Value:** [IReadOnlyList&lt;SemanticNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L108)

<a id="E2E_Engine_SemanticNode_InputPurpose_"></a>
<a id="E2E_Engine_SemanticNode_InputPurpose"></a>

### InputPurpose


```csharp
public string? InputPurpose { get; init; }
```


What a field takes: `username`, `password`, `one-time-code`, `generic-secret`, or `none`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L80)

<a id="E2E_Engine_SemanticNode_Level_"></a>
<a id="E2E_Engine_SemanticNode_Level"></a>

### Level


```csharp
public int? Level { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L82)

<a id="E2E_Engine_SemanticNode_Name_"></a>
<a id="E2E_Engine_SemanticNode_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


The accessible name, by the accname rules Playwright follows, or null when the node has none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L52)

<a id="E2E_Engine_SemanticNode_OwnText_"></a>
<a id="E2E_Engine_SemanticNode_OwnText"></a>

### OwnText


```csharp
public string? OwnText { get; init; }
```


The text the node owns, read as a line: its inline words stay in place, but its listed children's text is left out. The snapshot shows it.
Null when the node has no split: [Text](#E2E_Engine_SemanticNode_Text) then reads whole.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L61)

<a id="E2E_Engine_SemanticNode_Placeholder_"></a>
<a id="E2E_Engine_SemanticNode_Placeholder"></a>

### Placeholder


```csharp
public string? Placeholder { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L77)

<a id="E2E_Engine_SemanticNode_Rect_"></a>
<a id="E2E_Engine_SemanticNode_Rect"></a>

### Rect


```csharp
public BoundingBox? Rect { get; init; }
```


The node's box in viewport CSS pixels, when the platform measures one.

**Value:** [BoundingBox](/api/E2E.Engine/BoundingBox/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L106)

<a id="E2E_Engine_SemanticNode_Ref_"></a>
<a id="E2E_Engine_SemanticNode_Ref"></a>

### Ref


```csharp
public required string Ref { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L47)

<a id="E2E_Engine_SemanticNode_Role_"></a>
<a id="E2E_Engine_SemanticNode_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L49)

<a id="E2E_Engine_SemanticNode_Selection_"></a>
<a id="E2E_Engine_SemanticNode_Selection"></a>

### Selection


```csharp
public string? Selection { get; init; }
```


The text selected inside the focused field or editing host. Null for a caret, an unfocused field, or a secure one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L73)

<a id="E2E_Engine_SemanticNode_States_"></a>
<a id="E2E_Engine_SemanticNode_States"></a>

### States


```csharp
public NodeStates States { get; init; }
```


**Value:** [NodeStates](/api/E2E.Engine/NodeStates/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L84)

<a id="E2E_Engine_SemanticNode_TestId_"></a>
<a id="E2E_Engine_SemanticNode_TestId"></a>

### TestId


```csharp
public string? TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L75)

<a id="E2E_Engine_SemanticNode_Text_"></a>
<a id="E2E_Engine_SemanticNode_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


The rendered text, which locator reads and text queries use.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L55)

<a id="E2E_Engine_SemanticNode_Value_"></a>
<a id="E2E_Engine_SemanticNode_Value"></a>

### Value


```csharp
public string? Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L70)
