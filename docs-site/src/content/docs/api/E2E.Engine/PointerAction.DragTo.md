---
title: "PointerAction.DragTo"
description: "PointerAction.DragTo in E2E.Engine."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointerAction.DragTo"
  hidden: true
---

<a id="E2E_Engine_PointerAction_DragTo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record PointerAction.DragTo : PointerAction, IEquatable<PointerAction>, IEquatable<PointerAction.DragTo>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [PointerAction](/api/E2E.Engine/PointerAction/)

**Implements:** [IEquatable&lt;PointerAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;PointerAction.DragTo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)

## Constructors

<a id="E2E_Engine_PointerAction_DragTo__ctor_"></a>
<a id="E2E_Engine_PointerAction_DragTo__ctor_E2E_Engine_Point_"></a>

### DragTo(Point)


```csharp
public DragTo(Point Target)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [Point](/api/E2E.Engine/Point/) |  |

## Properties

<a id="E2E_Engine_PointerAction_DragTo_Target_"></a>
<a id="E2E_Engine_PointerAction_DragTo_Target"></a>

### Target


```csharp
public Point Target { get; init; }
```


**Value:** [Point](/api/E2E.Engine/Point/)
