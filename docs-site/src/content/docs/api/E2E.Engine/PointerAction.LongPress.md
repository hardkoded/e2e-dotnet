---
title: "PointerAction.LongPress"
description: "A hold. Without a duration the engine holds its own default."
editUrl: false
prev: false
next: false
sidebar:
  label: "PointerAction.LongPress"
  hidden: true
---

<a id="E2E_Engine_PointerAction_LongPress"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record PointerAction.LongPress : PointerAction, IEquatable<PointerAction>, IEquatable<PointerAction.LongPress>
```


A hold. Without a duration the engine holds its own default.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [PointerAction](/api/E2E.Engine/PointerAction/)

**Implements:** [IEquatable&lt;PointerAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;PointerAction.LongPress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/PointerAction.cs)

## Constructors

<a id="E2E_Engine_PointerAction_LongPress__ctor_"></a>
<a id="E2E_Engine_PointerAction_LongPress__ctor_System_Nullable_System_TimeSpan__"></a>

### LongPress(TimeSpan?)


```csharp
public LongPress(TimeSpan? Duration = null)
```


A hold. Without a duration the engine holds its own default.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Duration` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

## Properties

<a id="E2E_Engine_PointerAction_LongPress_Duration_"></a>
<a id="E2E_Engine_PointerAction_LongPress_Duration"></a>

### Duration


```csharp
public TimeSpan? Duration { get; init; }
```


**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)
