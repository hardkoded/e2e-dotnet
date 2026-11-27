---
title: "EngineNode"
description: "Upstream's SemanticNode (engine/contract.ts): one node of an engine's tree. Named EngineNode because the port's IEngine already has a SemanticNode, which car..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineNode"
  hidden: true
---

<a id="E2E_Engine_EngineNode"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineNode : IEquatable<EngineNode>
```


Upstream's `SemanticNode` (`engine/contract.ts`): one node of an engine's tree. Named `EngineNode` because the port's IEngine
already has a [SemanticNode](/api/E2E.Engine/SemanticNode/), which carries the port's own observation fields.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineNode&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_EngineNode__ctor_"></a>
<a id="E2E_Engine_EngineNode__ctor_E2E_Engine_NodeRef_System_String_System_String_System_Collections_Generic_IReadOnlyList_E2E_Engine_EngineNode__E2E_Engine_EngineNodeStates_System_String_System_String_System_String_System_String_System_String_System_Nullable_System_Int32__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__E2E_Engine_BoundingBox_System_Nullable_E2E_Engine_Point__System_String_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `EngineNode(NodeRef, string?, string?, IReadOnlyList<EngineNode>?, EngineNodeStates?, string?, string?, string?, string?, string?, int?, IReadOnlyDictionary<string, string>?, BoundingBox?, Point?, string?, IReadOnlyList<string>?)`


```csharp
public EngineNode(NodeRef Ref, string? Role = null, string? Name = null, IReadOnlyList<EngineNode>? Children = null, EngineNodeStates? States = null, string? Text = null, string? Value = null, string? Selection = null, string? TestId = null, string? InputPurpose = null, int? Level = null, IReadOnlyDictionary<string, string>? Attributes = null, BoundingBox? Rect = null, Point? Origin = null, string? Selector = null, IReadOnlyList<string>? FramePath = null)
```


Upstream's `SemanticNode` (`engine/contract.ts`): one node of an engine's tree. Named `EngineNode` because the port's IEngine
already has a [SemanticNode](/api/E2E.Engine/SemanticNode/), which carries the port's own observation fields.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Ref` | [NodeRef](/api/E2E.Engine/NodeRef/) | Node reference, valid against the observation it came from. |
| `Role` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Semantic role. |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Accessible name, cut at [Name](/api/E2E.Engine/ObservationLimits/#E2E_Engine_ObservationLimits_Name). |
| `Children` | [IReadOnlyList&lt;EngineNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | Child nodes. |
| `States` | [EngineNodeStates](/api/E2E.Engine/EngineNodeStates/) | Boolean states the platform reports. |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Visible text, cut at [Text](/api/E2E.Engine/ObservationLimits/#E2E_Engine_ObservationLimits_Text). |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Current input value; never reported for a secure field. |
| `Selection` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Text selected inside the node; absent when the selection is collapsed, and never reported for a secure field. |
| `TestId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The node's test id, when the platform gives it one. The `testId` query resolves against this field. |
| `InputPurpose` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What a field takes: `username`, `password`, `one-time-code`, `generic-secret`, or `none`. |
| `Level` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) | Heading level of a heading node; absent elsewhere. |
| `Attributes` | [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) | Platform attributes, what `getAttribute` reads. The `placeholder` query reads `placeholder` from here. |
| `Rect` | [BoundingBox](/api/E2E.Engine/BoundingBox/) | The node's box in the top-level viewport's CSS pixels. |
| `Origin` | `Point?` | The point an offset into the node is measured from, in &lt;code class="paramref"&gt;Rect&lt;/code&gt;'s space, when it is not its top-left corner. |
| `Selector` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Platform selector that addresses this node within its own document, when the platform has one. |
| `FramePath` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | Enclosing nested-document chain as selectors of each boundary node, outermost first. |

## Properties

<a id="E2E_Engine_EngineNode_Attributes_"></a>
<a id="E2E_Engine_EngineNode_Attributes"></a>

### Attributes


```csharp
public IReadOnlyDictionary<string, string>? Attributes { get; init; }
```


Platform attributes, what `getAttribute` reads. The `placeholder` query reads `placeholder` from here.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_EngineNode_Children_"></a>
<a id="E2E_Engine_EngineNode_Children"></a>

### Children


```csharp
public IReadOnlyList<EngineNode>? Children { get; init; }
```


Child nodes.

**Value:** [IReadOnlyList&lt;EngineNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineNode_FramePath_"></a>
<a id="E2E_Engine_EngineNode_FramePath"></a>

### FramePath


```csharp
public IReadOnlyList<string>? FramePath { get; init; }
```


Enclosing nested-document chain as selectors of each boundary node, outermost first.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineNode_InputPurpose_"></a>
<a id="E2E_Engine_EngineNode_InputPurpose"></a>

### InputPurpose


```csharp
public string? InputPurpose { get; init; }
```


What a field takes: `username`, `password`, `one-time-code`, `generic-secret`, or `none`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Level_"></a>
<a id="E2E_Engine_EngineNode_Level"></a>

### Level


```csharp
public int? Level { get; init; }
```


Heading level of a heading node; absent elsewhere.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EngineNode_Name_"></a>
<a id="E2E_Engine_EngineNode_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


Accessible name, cut at [Name](/api/E2E.Engine/ObservationLimits/#E2E_Engine_ObservationLimits_Name).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Origin_"></a>
<a id="E2E_Engine_EngineNode_Origin"></a>

### Origin


```csharp
public Point? Origin { get; init; }
```


The point an offset into the node is measured from, in &lt;code class="paramref"&gt;Rect&lt;/code&gt;'s space, when it is not its top-left corner.

**Value:** `Point?`

<a id="E2E_Engine_EngineNode_Rect_"></a>
<a id="E2E_Engine_EngineNode_Rect"></a>

### Rect


```csharp
public BoundingBox? Rect { get; init; }
```


The node's box in the top-level viewport's CSS pixels.

**Value:** [BoundingBox](/api/E2E.Engine/BoundingBox/)

<a id="E2E_Engine_EngineNode_Ref_"></a>
<a id="E2E_Engine_EngineNode_Ref"></a>

### Ref


```csharp
public NodeRef Ref { get; init; }
```


Node reference, valid against the observation it came from.

**Value:** [NodeRef](/api/E2E.Engine/NodeRef/)

<a id="E2E_Engine_EngineNode_Role_"></a>
<a id="E2E_Engine_EngineNode_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


Semantic role.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Selection_"></a>
<a id="E2E_Engine_EngineNode_Selection"></a>

### Selection


```csharp
public string? Selection { get; init; }
```


Text selected inside the node; absent when the selection is collapsed, and never reported for a secure field.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Selector_"></a>
<a id="E2E_Engine_EngineNode_Selector"></a>

### Selector


```csharp
public string? Selector { get; init; }
```


Platform selector that addresses this node within its own document, when the platform has one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_States_"></a>
<a id="E2E_Engine_EngineNode_States"></a>

### States


```csharp
public EngineNodeStates? States { get; init; }
```


Boolean states the platform reports.

**Value:** [EngineNodeStates](/api/E2E.Engine/EngineNodeStates/)

<a id="E2E_Engine_EngineNode_TestId_"></a>
<a id="E2E_Engine_EngineNode_TestId"></a>

### TestId


```csharp
public string? TestId { get; init; }
```


The node's test id, when the platform gives it one. The `testId` query resolves against this field.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Text_"></a>
<a id="E2E_Engine_EngineNode_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


Visible text, cut at [Text](/api/E2E.Engine/ObservationLimits/#E2E_Engine_ObservationLimits_Text).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Value_"></a>
<a id="E2E_Engine_EngineNode_Value"></a>

### Value


```csharp
public string? Value { get; init; }
```


Current input value; never reported for a secure field.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
