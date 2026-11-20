---
title: "Elements"
description: "Upstream's elements.ts. Its free functions live here as members (the class carries the module's name, as Model and Budget do)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Elements"
  hidden: true
---

<a id="E2E_Decision_Elements"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Elements
```


Upstream's `elements.ts`. Its free functions live here as members (the class carries the module's name, as `Model` and `Budget` do).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Decision_Elements_ActionSpace_"></a>
<a id="E2E_Decision_Elements_ActionSpace_E2E_StepExecutorContext_E2E_Decision_SpaceObservation_System_Boolean_"></a>

### ActionSpace(StepExecutorContext, SpaceObservation, bool)


```csharp
public static ActionSpace ActionSpace(StepExecutorContext ctx, SpaceObservation observation, bool canType)
```


Upstream's `actionSpace`. Builds the element table and bound targets from the newest observation.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ctx` | [StepExecutorContext](/api/E2E/StepExecutorContext/) |  |
| `observation` | [SpaceObservation](/api/E2E.Decision/SpaceObservation/) |  |
| `canType` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [ActionSpace](/api/E2E.Decision/ActionSpace/)

<a id="E2E_Decision_Elements_TargetKeyIndex_"></a>
<a id="E2E_Decision_Elements_TargetKeyIndex_System_String_"></a>

### TargetKeyIndex(string)


```csharp
public static string TargetKeyIndex(string key)
```


Upstream's `targetKeyIndex`. Element index behind a target key (`7` for both `7` and `7:2`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
