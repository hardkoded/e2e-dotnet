---
title: "NodeRef"
description: "Upstream's NodeRef (engine/contract.ts): a node's stable id and the revision of the observation or resolution that minted it."
editUrl: false
prev: false
next: false
sidebar:
  label: "NodeRef"
  hidden: true
---

<a id="E2E_Engine_NodeRef"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record NodeRef : IEquatable<NodeRef>
```


Upstream's `NodeRef` (`engine/contract.ts`): a node's stable id and the revision of the observation or resolution that minted it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;NodeRef&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_NodeRef__ctor_"></a>
<a id="E2E_Engine_NodeRef__ctor_System_String_System_String_"></a>

### NodeRef(string, string)


```csharp
public NodeRef(string Id, string Revision)
```


Upstream's `NodeRef` (`engine/contract.ts`): a node's stable id and the revision of the observation or resolution that minted it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Revision` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_NodeRef_Id_"></a>
<a id="E2E_Engine_NodeRef_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_NodeRef_Revision_"></a>
<a id="E2E_Engine_NodeRef_Revision"></a>

### Revision


```csharp
public string Revision { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
