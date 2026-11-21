---
title: "PointLocator"
description: "Locates drawn controls on the screenshot and remembers the ones a tap found: three looks per control, the column and row on the plain full screenshot, then a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointLocator"
  hidden: true
---

<a id="E2E_Decision_PointLocator"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class PointLocator
```


Locates drawn controls on the screenshot and remembers the ones a tap found: three looks per control, the column and row on the plain
full screenshot, then again on two ever tighter zoomed crops with a grid drawn on them; a field takes the first look alone. A tap that
changed the pixels around it found its control; the same control on the same page is tapped there again with no new looks (upstream's
`PointLocator`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Locator.cs)

## Constructors

<a id="E2E_Decision_PointLocator__ctor_"></a>
<a id="E2E_Decision_PointLocator__ctor_E2E_StepExecutorContext_E2E_Decision_AskDecision_E2E_Decision_Gates_"></a>

### PointLocator(StepExecutorContext, AskDecision, Gates)


```csharp
public PointLocator(StepExecutorContext ctx, AskDecision ask, Gates gates)
```


Locates drawn controls on the screenshot and remembers the ones a tap found: three looks per control, the column and row on the plain
full screenshot, then again on two ever tighter zoomed crops with a grid drawn on them; a field takes the first look alone. A tap that
changed the pixels around it found its control; the same control on the same page is tapped there again with no new looks (upstream's
`PointLocator`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `ask` | [AskDecision](/api/E2E.Decision/AskDecision/) |  |
| `gates` | [Gates](/api/E2E.Decision/Gates/) |  |

## Methods

<a id="E2E_Decision_PointLocator_Known_"></a>
<a id="E2E_Decision_PointLocator_Known_System_String_System_String_E2E_Decision_Aim_"></a>

### Known(string, string, Aim)


```csharp
public Point? Known(string path, string wanted, Aim aim)
```


A point already known for this control on this page.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `wanted` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `aim` | [Aim](/api/E2E.Decision/Aim/) |  |

**Returns:** `Point?`

<a id="E2E_Decision_PointLocator_LocateAsync_"></a>
<a id="E2E_Decision_PointLocator_LocateAsync_E2E_Decision_Screenshot_System_String_System_String_E2E_Decision_Aim_"></a>

### LocateAsync(Screenshot, string, string, Aim)


```csharp
public Task<Located?> LocateAsync(Screenshot screenshot, string path, string wanted, Aim aim)
```


The looks for the control or field, or null once the budget is spent.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `wanted` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `aim` | [Aim](/api/E2E.Decision/Aim/) |  |

**Returns:** [Task&lt;Located&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_PointLocator_Settle_"></a>
<a id="E2E_Decision_PointLocator_Settle_E2E_Decision_Screenshot_"></a>

### Settle(Screenshot?)


```csharp
public void Settle(Screenshot? screenshot)
```


Settles the last point tap against the newest pixels: changed means it landed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |

<a id="E2E_Decision_PointLocator_Tapped_"></a>
<a id="E2E_Decision_PointLocator_Tapped_System_String_System_String_E2E_Decision_Aim_E2E_Engine_Point_E2E_Decision_Screenshot_"></a>

### Tapped(string, string, Aim, Point, Screenshot)


```csharp
public void Tapped(string path, string wanted, Aim aim, Point point, Screenshot screenshot)
```


Records a point tap, to be settled by the next observation.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `wanted` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `aim` | [Aim](/api/E2E.Decision/Aim/) |  |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `screenshot` | [Screenshot](/api/E2E.Decision/Screenshot/) |  |
