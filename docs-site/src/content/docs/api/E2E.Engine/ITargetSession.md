---
title: "ITargetSession"
description: "Upstream's TargetSession (engine/surface.ts): the internal session surface the harness consumes for one attempt. It is produced only by CreateEngineSession(E..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ITargetSession"
  hidden: true
---

<a id="E2E_Engine_ITargetSession"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface ITargetSession
```


Upstream's `TargetSession` (`engine/surface.ts`): the internal session surface the harness consumes for one attempt. It is
produced only by [CreateEngineSession(EngineSessionOptions)](/api/E2E.Engine/EngineSessions/#E2E_Engine_EngineSessions_CreateEngineSession_E2E_Engine_EngineSessionOptions_), the engine-to-session adapter, so revision minting, staleness checks, and
capability gating exist once.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Properties

<a id="E2E_Engine_ITargetSession_Actions_"></a>
<a id="E2E_Engine_ITargetSession_Actions"></a>

### Actions


```csharp
IReadOnlySet<string> Actions { get; }
```


The action kinds the engine declared for `perform`; empty without the actions capability.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

<a id="E2E_Engine_ITargetSession_App_"></a>
<a id="E2E_Engine_ITargetSession_App"></a>

### App


```csharp
ISessionApp App { get; }
```


The app's session hooks.

**Value:** [ISessionApp](/api/E2E.Engine/ISessionApp/)

<a id="E2E_Engine_ITargetSession_AppLog_"></a>
<a id="E2E_Engine_ITargetSession_AppLog"></a>

### AppLog


```csharp
AppLogRoute AppLog { get; }
```


Where the engine's app log goes.

**Value:** [AppLogRoute](/api/E2E.Engine/AppLogRoute/)

<a id="E2E_Engine_ITargetSession_Artifacts_"></a>
<a id="E2E_Engine_ITargetSession_Artifacts"></a>

### Artifacts


```csharp
ISessionArtifacts Artifacts { get; }
```


The app's artifact hooks.

**Value:** [ISessionArtifacts](/api/E2E.Engine/ISessionArtifacts/)

<a id="E2E_Engine_ITargetSession_CaptureState_"></a>
<a id="E2E_Engine_ITargetSession_CaptureState"></a>

### CaptureState


```csharp
Func<OperationContext, Task<EngineState>>? CaptureState { get; }
```


Captures immutable app state for a session envelope; null without the state capability.

**Value:** [Func&lt;OperationContext, Task&lt;EngineState&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_ITargetSession_Environment_"></a>
<a id="E2E_Engine_ITargetSession_Environment"></a>

### Environment


```csharp
EnvironmentFacts Environment { get; }
```


What the engine said the attempt runs on, bounded.

**Value:** [EnvironmentFacts](/api/E2E.Engine/EnvironmentFacts/)

<a id="E2E_Engine_ITargetSession_Keyboard_"></a>
<a id="E2E_Engine_ITargetSession_Keyboard"></a>

### Keyboard


```csharp
ISessionKeyboard Keyboard { get; }
```


Input to whatever holds focus; each member fails with `UNSUPPORTED_CAPABILITY` when the engine lacks it.

**Value:** [ISessionKeyboard](/api/E2E.Engine/ISessionKeyboard/)

<a id="E2E_Engine_ITargetSession_PointerActions_"></a>
<a id="E2E_Engine_ITargetSession_PointerActions"></a>

### PointerActions


```csharp
IReadOnlySet<string> PointerActions { get; }
```


The pointer action kinds the engine declared for `performAt`; empty without the pointer capability.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

<a id="E2E_Engine_ITargetSession_RestoreState_"></a>
<a id="E2E_Engine_ITargetSession_RestoreState"></a>

### RestoreState


```csharp
Func<EngineState, OperationContext, Task>? RestoreState { get; }
```


Replaces current app state with a captured state; null without the state capability.

**Value:** [Func&lt;EngineState, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_ITargetSession_Screens_"></a>
<a id="E2E_Engine_ITargetSession_Screens"></a>

### Screens


```csharp
ScreenRoute Screens { get; }
```


Where the screens of the attempt go for its trace.

**Value:** [ScreenRoute](/api/E2E.Engine/ScreenRoute/)

<a id="E2E_Engine_ITargetSession_TapModifiers_"></a>
<a id="E2E_Engine_ITargetSession_TapModifiers"></a>

### TapModifiers


```csharp
bool TapModifiers { get; }
```


Whether the engine declared tap modifiers, so the tap actions may carry modifier keys.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_ITargetSession_Verbs_"></a>
<a id="E2E_Engine_ITargetSession_Verbs"></a>

### Verbs


```csharp
IReadOnlySet<string> Verbs { get; }
```


The grammar verbs the engine can honor, derived from its declared action kinds and hooks.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

## Methods

<a id="E2E_Engine_ITargetSession_CloseAsync_"></a>
<a id="E2E_Engine_ITargetSession_CloseAsync_E2E_Engine_OperationContext_"></a>

### CloseAsync(OperationContext)


```csharp
Task CloseAsync(OperationContext operation)
```


Ends the attempt's isolation within the operation's budget. Idempotent.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ITargetSession_LocateAsync_"></a>
<a id="E2E_Engine_ITargetSession_LocateAsync_E2E_LocatorExpression_E2E_Engine_OperationContext_"></a>

### LocateAsync(LocatorExpression, OperationContext)


```csharp
Task<IReadOnlyList<NodeRef>> LocateAsync(LocatorExpression expression, OperationContext operation)
```


Resolves a query immediately; the runner owns query polling and strictness.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [LocatorExpression](/api/E2E/LocatorExpression/) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;NodeRef&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_ITargetSession_ObserveAsync_"></a>
<a id="E2E_Engine_ITargetSession_ObserveAsync_E2E_Engine_OperationContext_E2E_Engine_ObserveOptions_"></a>

### ObserveAsync(OperationContext, ObserveOptions?)


```csharp
Task<SessionObservation> ObserveAsync(OperationContext operation, ObserveOptions? options = null)
```


Captures one atomic agent observation; the harness redacts it downstream.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `options` | [ObserveOptions](/api/E2E.Engine/ObserveOptions/) |  |

**Returns:** [Task&lt;SessionObservation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_ITargetSession_PerformAsync_"></a>
<a id="E2E_Engine_ITargetSession_PerformAsync_E2E_Engine_NodeRef_E2E_Engine_LocatorAction_E2E_Engine_OperationContext_"></a>

### PerformAsync(NodeRef, LocatorAction, OperationContext)


```csharp
Task PerformAsync(NodeRef reference, LocatorAction action, OperationContext operation)
```


Performs exactly one action on a located or observed node.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reference` | [NodeRef](/api/E2E.Engine/NodeRef/) |  |
| `action` | [LocatorAction](/api/E2E.Engine/LocatorAction/) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ITargetSession_PerformAtAsync_"></a>
<a id="E2E_Engine_ITargetSession_PerformAtAsync_E2E_Engine_Point_E2E_Engine_PointerAction_E2E_Engine_OperationContext_"></a>

### PerformAtAsync(Point, PointerAction, OperationContext)


```csharp
Task PerformAtAsync(Point point, PointerAction action, OperationContext operation)
```


Performs one pointer action at a viewport point, in CSS pixels, with no node behind it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `action` | [PointerAction](/api/E2E.Engine/PointerAction/) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ITargetSession_ReadAsync_"></a>
<a id="E2E_Engine_ITargetSession_ReadAsync_E2E_Engine_NodeRef_E2E_Engine_OperationContext_"></a>

### ReadAsync(NodeRef, OperationContext)


```csharp
Task<EngineNode> ReadAsync(NodeRef reference, OperationContext operation)
```


Reads one node from the resolution that minted its ref.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reference` | [NodeRef](/api/E2E.Engine/NodeRef/) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task&lt;EngineNode&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_ITargetSession_SettleAsync_"></a>
<a id="E2E_Engine_ITargetSession_SettleAsync_E2E_Engine_OperationContext_"></a>

### SettleAsync(OperationContext)


```csharp
Task SettleAsync(OperationContext operation)
```


Rethrows a failure the engine collected on a path no step awaited; a no-op without the hook.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_ITargetSession_SwipeAsync_"></a>
<a id="E2E_Engine_ITargetSession_SwipeAsync_E2E_Engine_ScrollDirection_System_Nullable_E2E_Engine_Momentum__E2E_Engine_OperationContext_"></a>

### SwipeAsync(ScrollDirection, Momentum?, OperationContext)


```csharp
Task SwipeAsync(ScrollDirection direction, Momentum? momentum, OperationContext operation)
```


Performs a viewport swipe: `perform(root, swipe)`, observing first when no observation has named the root yet.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `momentum` | `Momentum?` |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
