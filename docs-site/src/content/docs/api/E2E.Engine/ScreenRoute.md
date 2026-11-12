---
title: "ScreenRoute"
description: "Upstream's ScreenRoute (engine/session.ts): where the screens of an attempt go for its trace. Without a sink they are dropped, and nothing waits for them."
editUrl: false
prev: false
next: false
sidebar:
  label: "ScreenRoute"
  hidden: true
---

<a id="E2E_Engine_ScreenRoute"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class ScreenRoute
```


Upstream's `ScreenRoute` (`engine/session.ts`): where the screens of an attempt go for its trace. Without a sink they are dropped,
and nothing waits for them.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Properties

<a id="E2E_Engine_ScreenRoute_Traced_"></a>
<a id="E2E_Engine_ScreenRoute_Traced"></a>

### Traced


```csharp
public bool Traced { get; }
```


Whether the attempt keeps a trace; without one nothing is pushed or routed, and the engine is not asked.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_Engine_ScreenRoute_Push_"></a>
<a id="E2E_Engine_ScreenRoute_Push_E2E_Engine_EngineSnapshot_"></a>

### Push(EngineSnapshot)


```csharp
public void Push(EngineSnapshot snapshot)
```


Takes one engine snapshot, checked: a malformed one is dropped.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `snapshot` | [EngineSnapshot](/api/E2E.Engine/EngineSnapshot/) |  |

<a id="E2E_Engine_ScreenRoute_Route_"></a>
<a id="E2E_Engine_ScreenRoute_Route_System_Action_E2E_Engine_SessionObservation__"></a>

### `Route(Action<SessionObservation>?)`


```csharp
public void Route(Action<SessionObservation>? sink)
```


Sends every screen from now on to &lt;code class="paramref"&gt;sink&lt;/code&gt;; null drops them again.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sink` | [Action&lt;SessionObservation&gt;](https://learn.microsoft.com/dotnet/api/system.action-1) |  |
