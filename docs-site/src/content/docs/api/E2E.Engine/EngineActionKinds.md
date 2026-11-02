---
title: "EngineActionKinds"
description: "The action kinds of the engine contract, by upstream's names, in contract order: what an engine declares in ActionKinds and PointerActionKinds. Upstream's LO..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineActionKinds"
  hidden: true
---

<a id="E2E_Engine_EngineActionKinds"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class EngineActionKinds
```


The action kinds of the engine contract, by upstream's names, in contract order: what an engine declares in
[ActionKinds](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_ActionKinds) and [PointerActionKinds](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_PointerActionKinds). Upstream's
`LOCATOR_ACTION_KINDS` and `POINTER_ACTION_KINDS`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)

## Properties

<a id="E2E_Engine_EngineActionKinds_Locator_"></a>
<a id="E2E_Engine_EngineActionKinds_Locator"></a>

### Locator


```csharp
public static IReadOnlyList<string> Locator { get; }
```


Every kind of [LocatorAction](/api/E2E.Engine/LocatorAction/).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineActionKinds_Pointer_"></a>
<a id="E2E_Engine_EngineActionKinds_Pointer"></a>

### Pointer


```csharp
public static IReadOnlyList<string> Pointer { get; }
```


Every kind of [PointerAction](/api/E2E.Engine/PointerAction/).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Engine_EngineActionKinds_Of_"></a>
<a id="E2E_Engine_EngineActionKinds_Of_E2E_Engine_LocatorAction_"></a>

### Of(LocatorAction)


```csharp
public static string Of(LocatorAction action)
```


The upstream name of a locator action's kind. [LocatorAction.PressSequentially](/api/E2E.Engine/LocatorAction.PressSequentially/) is upstream's `focus` followed by
the keyboard, so it needs the engine to declare `focus`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `action` | [LocatorAction](/api/E2E.Engine/LocatorAction/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineActionKinds_Of_E2E_Engine_PointerAction_"></a>

### Of(PointerAction)


```csharp
public static string Of(PointerAction action)
```


The upstream name of a pointer action's kind.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `action` | [PointerAction](/api/E2E.Engine/PointerAction/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
