---
title: "EngineNodeStates"
description: "Upstream's SemanticNode.states: the flags of a node the harness reads."
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


Upstream's `SemanticNode.states`: the flags of a node the harness reads.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineNodeStates&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_EngineNodeStates__ctor_"></a>
<a id="E2E_Engine_EngineNodeStates__ctor_System_Nullable_System_Boolean__System_Nullable_System_Boolean__"></a>

### EngineNodeStates(bool?, bool?)


```csharp
public EngineNodeStates(bool? Secure = null, bool? Hidden = null)
```


Upstream's `SemanticNode.states`: the flags of a node the harness reads.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Secure` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `Hidden` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

## Properties

<a id="E2E_Engine_EngineNodeStates_Hidden_"></a>
<a id="E2E_Engine_EngineNodeStates_Hidden"></a>

### Hidden


```csharp
public bool? Hidden { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineNodeStates_Secure_"></a>
<a id="E2E_Engine_EngineNodeStates_Secure"></a>

### Secure


```csharp
public bool? Secure { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)
