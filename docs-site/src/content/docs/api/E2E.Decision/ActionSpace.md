---
title: "ActionSpace"
description: "The element table and the targets bound to it (upstream's ActionSpace)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActionSpace"
  hidden: true
---

<a id="E2E_Decision_ActionSpace"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class ActionSpace
```


The element table and the targets bound to it (upstream's `ActionSpace`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Elements.cs)

## Properties

<a id="E2E_Decision_ActionSpace_Controls_"></a>
<a id="E2E_Decision_ActionSpace_Controls"></a>

### Controls


```csharp
public required IReadOnlyList<Control> Controls { get; init; }
```


**Value:** [IReadOnlyList&lt;Control&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_ActionSpace_Destinations_"></a>
<a id="E2E_Decision_ActionSpace_Destinations"></a>

### Destinations


```csharp
public required IReadOnlyDictionary<string, Destination> Destinations { get; init; }
```


Where a `drag` can drop, by element key; empty when nothing can receive a drop.

**Value:** [IReadOnlyDictionary&lt;string, Destination&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_ActionSpace_Elements_"></a>
<a id="E2E_Decision_ActionSpace_Elements"></a>

### Elements


```csharp
public required IReadOnlyList<Element> Elements { get; init; }
```


**Value:** [IReadOnlyList&lt;Element&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_ActionSpace_Fingerprint_"></a>
<a id="E2E_Decision_ActionSpace_Fingerprint"></a>

### Fingerprint


```csharp
public string Fingerprint { get; init; }
```


Stable hash of path, tree content with node ids removed, and which nodes are in view.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_ActionSpace_Omitted_"></a>
<a id="E2E_Decision_ActionSpace_Omitted"></a>

### Omitted


```csharp
public int Omitted { get; init; }
```


Elements left out to stay under the per-question cap; scrolling can bring them into view.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_ActionSpace_PageText_"></a>
<a id="E2E_Decision_ActionSpace_PageText"></a>

### PageText


```csharp
public string PageText { get; init; }
```


Non-interactive page text from the tree, without node ids, clipped to 6000 chars.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_ActionSpace_Statuses_"></a>
<a id="E2E_Decision_ActionSpace_Statuses"></a>

### Statuses


```csharp
public IReadOnlyList<string> Statuses { get; init; }
```


Live regions (status, alert, log) as `name: text`, the lines a completion check reads first.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_ActionSpace_TapAt_"></a>
<a id="E2E_Decision_ActionSpace_TapAt"></a>

### TapAt


```csharp
public bool TapAt { get; init; }
```


Whether `tap_at` is offered: a screenshot, the `tapAt` verb, and a step that locates.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_ActionSpace_Targets_"></a>
<a id="E2E_Decision_ActionSpace_Targets"></a>

### Targets


```csharp
public required IReadOnlyDictionary<Operation, IReadOnlyDictionary<string, Target>> Targets { get; init; }
```


Per operation: target key to target. Only operations with at least one target appear.

**Value:** [IReadOnlyDictionary&lt;Operation, IReadOnlyDictionary&lt;string, Target&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_ActionSpace_TypeAt_"></a>
<a id="E2E_Decision_ActionSpace_TypeAt"></a>

### TypeAt


```csharp
public bool TypeAt { get; init; }
```


Whether `type_at` is offered: `tap_at` is, and the engine types into whatever holds focus.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_Decision_ActionSpace_ControlNamed_"></a>
<a id="E2E_Decision_ActionSpace_ControlNamed_System_String_"></a>

### ControlNamed(string)


```csharp
public Control? ControlNamed(string choice)
```


Upstream's `control(choice)` (named apart for the same reason as [OperationNamed(string)](#E2E_Decision_ActionSpace_OperationNamed_System_String_)): the control an answer names, typed; null for anything not offered.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `choice` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `Control?`

<a id="E2E_Decision_ActionSpace_Element_"></a>
<a id="E2E_Decision_ActionSpace_Element_System_String_"></a>

### Element(string)


```csharp
public Element? Element(string key)
```


Upstream's `element(key)`: the element behind a target key, for labels and field descriptions.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Element](/api/E2E.Decision/Element/)

<a id="E2E_Decision_ActionSpace_OperationNamed_"></a>
<a id="E2E_Decision_ActionSpace_OperationNamed_System_String_"></a>

### OperationNamed(string)


```csharp
public Operation? OperationNamed(string choice)
```


Upstream's `operation(choice)` (named apart here: a member cannot share the name of the [Operation](/api/E2E.Decision/Operation/) type it
returns). The operation an answer names, typed; null for anything not offered.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `choice` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `Operation?`
