---
title: "EngineTabs"
description: "Upstream's EngineTabs (engine/index.ts): the tabs a surface has open, as a snapshot reports them."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineTabs"
  hidden: true
---

<a id="E2E_Engine_EngineTabs"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineTabs : IEquatable<EngineTabs>
```


Upstream's `EngineTabs` (`engine/index.ts`): the tabs a surface has open, as a snapshot reports them.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineTabs&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/SpiContract.cs)

## Constructors

<a id="E2E_Engine_EngineTabs__ctor_"></a>
<a id="E2E_Engine_EngineTabs__ctor_System_Int32_System_Int32_"></a>

### EngineTabs(int, int)


```csharp
public EngineTabs(int Count, int Active)
```


Upstream's `EngineTabs` (`engine/index.ts`): the tabs a surface has open, as a snapshot reports them.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Count` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | How many tabs are open: 2 or more. |
| `Active` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The zero-based position, in the order the tabs opened, of the tab the snapshot's root reads. |

## Properties

<a id="E2E_Engine_EngineTabs_Active_"></a>
<a id="E2E_Engine_EngineTabs_Active"></a>

### Active


```csharp
public int Active { get; init; }
```


The zero-based position, in the order the tabs opened, of the tab the snapshot's root reads.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EngineTabs_Count_"></a>
<a id="E2E_Engine_EngineTabs_Count"></a>

### Count


```csharp
public int Count { get; init; }
```


How many tabs are open: 2 or more.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
