---
title: "LocatorAction.DragTo"
description: "Drags the node onto <code class=\"paramref\">Target</code>, a node of the same observation."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.DragTo"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_DragTo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.DragTo : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.DragTo>
```


Drags the node onto &lt;code class="paramref"&gt;Target&lt;/code&gt;, a node of the same observation.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.DragTo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L30)

## Constructors

<a id="E2E_Engine_LocatorAction_DragTo__ctor_"></a>
<a id="E2E_Engine_LocatorAction_DragTo__ctor_E2E_Engine_SemanticNode_"></a>

### DragTo(SemanticNode)


```csharp
public DragTo(SemanticNode Target)
```


Drags the node onto &lt;code class="paramref"&gt;Target&lt;/code&gt;, a node of the same observation.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [SemanticNode](/api/E2E.Engine/SemanticNode/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L30)

## Properties

<a id="E2E_Engine_LocatorAction_DragTo_Target_"></a>
<a id="E2E_Engine_LocatorAction_DragTo_Target"></a>

### Target


```csharp
public SemanticNode Target { get; init; }
```


**Value:** [SemanticNode](/api/E2E.Engine/SemanticNode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L30)
