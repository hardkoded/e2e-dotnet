---
title: "LocatorAction.LongPress"
description: "A press held on the node for Duration, or for the engine's own default hold when it is null."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.LongPress"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_LongPress"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.LongPress : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.LongPress>
```


A press held on the node for &lt;code class="paramref"&gt;Duration&lt;/code&gt;, or for the engine's own default hold when it is null.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.LongPress&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L24)

## Constructors

<a id="E2E_Engine_LocatorAction_LongPress__ctor_"></a>
<a id="E2E_Engine_LocatorAction_LongPress__ctor_System_Nullable_System_TimeSpan__"></a>

### LongPress(TimeSpan?)


```csharp
public LongPress(TimeSpan? Duration = null)
```


A press held on the node for &lt;code class="paramref"&gt;Duration&lt;/code&gt;, or for the engine's own default hold when it is null.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Duration` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L24)

## Properties

<a id="E2E_Engine_LocatorAction_LongPress_Duration_"></a>
<a id="E2E_Engine_LocatorAction_LongPress_Duration"></a>

### Duration


```csharp
public TimeSpan? Duration { get; init; }
```


**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L24)
