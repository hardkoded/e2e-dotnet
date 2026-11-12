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
<a id="E2E_Engine_EngineNode__ctor_E2E_Engine_NodeRef_System_String_System_String_System_Collections_Generic_IReadOnlyList_E2E_Engine_EngineNode__E2E_Engine_EngineNodeStates_"></a>

### `EngineNode(NodeRef, string?, string?, IReadOnlyList<EngineNode>?, EngineNodeStates?)`


```csharp
public EngineNode(NodeRef Ref, string? Role = null, string? Name = null, IReadOnlyList<EngineNode>? Children = null, EngineNodeStates? States = null)
```


Upstream's `SemanticNode` (`engine/contract.ts`): one node of an engine's tree. Named `EngineNode` because the port's IEngine
already has a [SemanticNode](/api/E2E.Engine/SemanticNode/), which carries the port's own observation fields.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Ref` | [NodeRef](/api/E2E.Engine/NodeRef/) |  |
| `Role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Children` | [IReadOnlyList&lt;EngineNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `States` | [EngineNodeStates](/api/E2E.Engine/EngineNodeStates/) |  |

## Properties

<a id="E2E_Engine_EngineNode_Children_"></a>
<a id="E2E_Engine_EngineNode_Children"></a>

### Children


```csharp
public IReadOnlyList<EngineNode>? Children { get; init; }
```


**Value:** [IReadOnlyList&lt;EngineNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineNode_Name_"></a>
<a id="E2E_Engine_EngineNode_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_Ref_"></a>
<a id="E2E_Engine_EngineNode_Ref"></a>

### Ref


```csharp
public NodeRef Ref { get; init; }
```


**Value:** [NodeRef](/api/E2E.Engine/NodeRef/)

<a id="E2E_Engine_EngineNode_Role_"></a>
<a id="E2E_Engine_EngineNode_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineNode_States_"></a>
<a id="E2E_Engine_EngineNode_States"></a>

### States


```csharp
public EngineNodeStates? States { get; init; }
```


**Value:** [EngineNodeStates](/api/E2E.Engine/EngineNodeStates/)
