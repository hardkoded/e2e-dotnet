---
title: "IEngineSession"
description: "One attempt's session with the app. Disposed at the end of the attempt, after afterEach."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngineSession"
  hidden: true
---

<a id="E2E_Engine_IEngineSession"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngineSession : IAsyncDisposable
```


One attempt's session with the app. Disposed at the end of the attempt, after `afterEach`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_IEngineSession_ActionKinds_"></a>
<a id="E2E_Engine_IEngineSession_ActionKinds"></a>

### ActionKinds


```csharp
IReadOnlyCollection<string> ActionKinds { get; }
```


The locator action kinds the engine declared for `PerformAsync`, by upstream's names ([Locator](/api/E2E.Engine/EngineActionKinds/#E2E_Engine_EngineActionKinds_Locator)).
The harness refuses any other kind with `UNSUPPORTED_CAPABILITY` before it resolves the node. Upstream's `actions`. The
default declares every kind, so an engine that does not narrow it keeps compiling.

**Value:** [IReadOnlyCollection&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)

<a id="E2E_Engine_IEngineSession_CanDismissKeyboard_"></a>
<a id="E2E_Engine_IEngineSession_CanDismissKeyboard"></a>

### CanDismissKeyboard


```csharp
bool CanDismissKeyboard { get; }
```


Whether the engine hides an on-screen keyboard ([DismissKeyboardAsync(CancellationToken)](#E2E_Engine_IEngineSession_DismissKeyboardAsync_System_Threading_CancellationToken_)). The harness lists `dismissKeyboard` in the
target's verbs only for an engine that says true. .NET-only: upstream's `keyboard.dismiss` is an optional hook, and a hook that is
defined is what lists the verb; C# has no optional member to test, so this declares it. The default is false, so an engine written
before the member keeps compiling.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_IEngineSession_CanLocate_"></a>
<a id="E2E_Engine_IEngineSession_CanLocate"></a>

### CanLocate


```csharp
bool CanLocate { get; }
```


Whether `LocateAsync` is implemented. A session that says false has its locators resolved in core over
[ObserveAsync(CancellationToken)](#E2E_Engine_IEngineSession_ObserveAsync_System_Threading_CancellationToken_). .NET-only and transitional: upstream's engines all locate, and this goes once the web and document
engines do.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_IEngineSession_PointerActionKinds_"></a>
<a id="E2E_Engine_IEngineSession_PointerActionKinds"></a>

### PointerActionKinds


```csharp
IReadOnlyCollection<string> PointerActionKinds { get; }
```


The pointer action kinds the engine declared for `PerformAtAsync` ([Pointer](/api/E2E.Engine/EngineActionKinds/#E2E_Engine_EngineActionKinds_Pointer)), empty for an
engine with no pointer. Upstream's `pointerActions`.

**Value:** [IReadOnlyCollection&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)

<a id="E2E_Engine_IEngineSession_Route_"></a>
<a id="E2E_Engine_IEngineSession_Route"></a>

### Route


```csharp
string Route { get; }
```


Location of the current screen: its path, query and fragment, without the origin.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_IEngineSession_BackAsync_"></a>
<a id="E2E_Engine_IEngineSession_BackAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### BackAsync(OperationContext, CancellationToken)


```csharp
Task BackAsync(OperationContext context, CancellationToken cancellationToken)
```


[BackAsync(CancellationToken)](#E2E_Engine_IEngineSession_BackAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `back(operation)`. The default drops the
context, as `PerformAsync` does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_BackAsync_System_Threading_CancellationToken_"></a>

### BackAsync(CancellationToken)


```csharp
Task BackAsync(CancellationToken cancellationToken)
```


Goes back one entry in the session history. With no earlier entry it does nothing.
Engines that declare [History](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_History) implement it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_CaptureStateAsync_"></a>
<a id="E2E_Engine_IEngineSession_CaptureStateAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### CaptureStateAsync(OperationContext, CancellationToken)


```csharp
Task<EngineState> CaptureStateAsync(OperationContext context, CancellationToken cancellationToken)
```


[CaptureStateAsync(CancellationToken)](#E2E_Engine_IEngineSession_CaptureStateAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `state.capture`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EngineState&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_CaptureStateAsync_System_Threading_CancellationToken_"></a>

### CaptureStateAsync(CancellationToken)


```csharp
Task<EngineState> CaptureStateAsync(CancellationToken cancellationToken)
```


Captures the app's state (cookies and storage) as a payload another session can restore. The payload's `Data` is a mutable
`JsonNode`, so a caller must not change it between capture and restore. Upstream's optional
`captureState`; a session that keeps none fails with `UNSUPPORTED_CAPABILITY`, as upstream's `session.save()` does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EngineState&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ClearStateAsync_"></a>
<a id="E2E_Engine_IEngineSession_ClearStateAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ClearStateAsync(OperationContext, CancellationToken)


```csharp
Task ClearStateAsync(OperationContext context, CancellationToken cancellationToken)
```


[ClearStateAsync(CancellationToken)](#E2E_Engine_IEngineSession_ClearStateAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `session.reset`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_ClearStateAsync_System_Threading_CancellationToken_"></a>

### ClearStateAsync(CancellationToken)


```csharp
Task ClearStateAsync(CancellationToken cancellationToken)
```


Discards cookies, storage, and history, and opens a blank document.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_CloseTabAsync_"></a>
<a id="E2E_Engine_IEngineSession_CloseTabAsync_System_Int32_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### CloseTabAsync(int, OperationContext, CancellationToken)


```csharp
Task CloseTabAsync(int index, OperationContext context, CancellationToken cancellationToken)
```


Closes the open tab at &lt;code class="paramref"&gt;index&lt;/code&gt;, counted as `SwitchTabAsync` counts
them. When it was the tab shown, the engine shows another, such as the tab that opened it. An index with no open tab, or the last open
tab, is an `ACTION_FAILED` the model reads. Engines that declare [CloseTab](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_CloseTab) implement it.
Upstream's `session.closeTab`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_DismissKeyboardAsync_"></a>
<a id="E2E_Engine_IEngineSession_DismissKeyboardAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### DismissKeyboardAsync(OperationContext, CancellationToken)


```csharp
Task DismissKeyboardAsync(OperationContext context, CancellationToken cancellationToken)
```


[DismissKeyboardAsync(CancellationToken)](#E2E_Engine_IEngineSession_DismissKeyboardAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `keyboard.dismiss`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_DismissKeyboardAsync_System_Threading_CancellationToken_"></a>

### DismissKeyboardAsync(CancellationToken)


```csharp
Task DismissKeyboardAsync(CancellationToken cancellationToken)
```


Hides an on-screen keyboard, so a control it covered can be reached. Platforms without one leave [CanDismissKeyboard](#E2E_Engine_IEngineSession_CanDismissKeyboard)
false. The default refuses with `UNSUPPORTED_CAPABILITY` (.NET-only: it keeps an engine written before the member compiling).
Upstream's `keyboard.dismiss`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_LocateAsync_"></a>
<a id="E2E_Engine_IEngineSession_LocateAsync_E2E_LocatorExpression_System_TimeSpan_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### LocateAsync(LocatorExpression, TimeSpan, OperationContext, CancellationToken)


```csharp
Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, OperationContext context, CancellationToken cancellationToken)
```


`LocateAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `locate(expression, operation)`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [LocatorExpression](/api/E2E/LocatorExpression/) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SemanticNode&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_LocateAsync_E2E_LocatorExpression_System_TimeSpan_System_Threading_CancellationToken_"></a>

### LocateAsync(LocatorExpression, TimeSpan, CancellationToken)


```csharp
Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken)
```


Resolves a locator against the screen as it is now and returns every match in document order. Polling and strictness stay with
the harness. A screen the engine cannot read yet matches nothing, so the harness keeps polling. &lt;code class="paramref"&gt;timeout&lt;/code&gt; is the
budget left for the resolve, which an engine uses to tell a screen that is still arriving from one that never rendered. A
waiting call's last locate can get more than its deadline leaves: as long as the slowest one it has seen, within the test timeout.
An engine ends the resolve at this budget, with `OPERATION_TIMEOUT`, and does not let a hung device hold it longer. A
`union` expression matches either branch in document order, without duplicate nodes. Engines that declare
[Location](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Location) and set [CanLocate](#E2E_Engine_IEngineSession_CanLocate) implement it. Upstream's `locate`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [LocatorExpression](/api/E2E/LocatorExpression/) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;SemanticNode&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ObserveAsync_"></a>
<a id="E2E_Engine_IEngineSession_ObserveAsync_E2E_Engine_ObserveOptions_System_TimeSpan_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ObserveAsync(ObserveOptions?, TimeSpan, OperationContext, CancellationToken)


```csharp
Task<Observation> ObserveAsync(ObserveOptions? options, TimeSpan timeout, OperationContext context, CancellationToken cancellationToken)
```


`ObserveAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `observe(operation, { pixels, comparable })`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ObserveOptions](/api/E2E.Engine/ObserveOptions/) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ObserveAsync_E2E_Engine_ObserveOptions_System_TimeSpan_System_Threading_CancellationToken_"></a>

### ObserveAsync(ObserveOptions?, TimeSpan, CancellationToken)


```csharp
Task<Observation> ObserveAsync(ObserveOptions? options, TimeSpan timeout, CancellationToken cancellationToken)
```


`ObserveAsync` with &lt;code class="paramref"&gt;options&lt;/code&gt;: upstream's
`observe(operation, { pixels, comparable })`. With [Pixels](/api/E2E.Engine/ObserveOptions/#E2E_Engine_ObserveOptions_Pixels) the observation carries the screen's
[Pixels](/api/E2E.Engine/Observation/#E2E_Engine_Observation_Pixels), taken under the same budget as the tree. An engine that takes none returns the tree alone, and the
default does (.NET-only: it keeps the engines that capture no pixels compiling).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ObserveOptions](/api/E2E.Engine/ObserveOptions/) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ObserveAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ObserveAsync(OperationContext, CancellationToken)


```csharp
Task<Observation> ObserveAsync(OperationContext context, CancellationToken cancellationToken)
```


[ObserveAsync(CancellationToken)](#E2E_Engine_IEngineSession_ObserveAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `observe(operation)`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ObserveAsync_System_Threading_CancellationToken_"></a>

### ObserveAsync(CancellationToken)


```csharp
Task<Observation> ObserveAsync(CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ObserveAsync_System_TimeSpan_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ObserveAsync(TimeSpan, OperationContext, CancellationToken)


```csharp
Task<Observation> ObserveAsync(TimeSpan timeout, OperationContext context, CancellationToken cancellationToken)
```


`ObserveAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `observe(operation)` with its `operation.timeoutMs`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ObserveAsync_System_TimeSpan_System_Threading_CancellationToken_"></a>

### ObserveAsync(TimeSpan, CancellationToken)


```csharp
Task<Observation> ObserveAsync(TimeSpan timeout, CancellationToken cancellationToken)
```


[ObserveAsync(CancellationToken)](#E2E_Engine_IEngineSession_ObserveAsync_System_Threading_CancellationToken_) within &lt;code class="paramref"&gt;timeout&lt;/code&gt;, the operation budget left for the read: upstream's
`observe(operation)`, whose `operation.timeoutMs` bounds every device command of the observation. An engine that bounds
its reads overrides it and ends the read at the budget with `OPERATION_TIMEOUT`; the default drops the budget and observes
as [ObserveAsync(CancellationToken)](#E2E_Engine_IEngineSession_ObserveAsync_System_Threading_CancellationToken_) does (.NET-only: it keeps the engines that bound nothing compiling).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Observation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_OpenAsync_"></a>
<a id="E2E_Engine_IEngineSession_OpenAsync_System_String_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### OpenAsync(string, OperationContext, CancellationToken)


```csharp
Task OpenAsync(string url, OperationContext context, CancellationToken cancellationToken)
```


`OpenAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `session.open`. The default drops the context, as `PerformAsync` does (.NET-only: it keeps the engines that ignore it compiling).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_OpenAsync_System_String_System_Threading_CancellationToken_"></a>

### OpenAsync(string, CancellationToken)


```csharp
Task OpenAsync(string url, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_PerformAsync_"></a>
<a id="E2E_Engine_IEngineSession_PerformAsync_E2E_Engine_SemanticNode_E2E_Engine_LocatorAction_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### PerformAsync(SemanticNode, LocatorAction, OperationContext, CancellationToken)


```csharp
Task PerformAsync(SemanticNode node, LocatorAction action, OperationContext context, CancellationToken cancellationToken)
```


`PerformAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;, who acts: upstream's
`perform(ref, action, operation)`. An engine that acts differently for a test's step than for the agent (a device that settles
only for the agent) overrides it. The default drops the context (.NET-only: it keeps the engines that ignore it compiling, as upstream's
web and document engines do).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `node` | [SemanticNode](/api/E2E.Engine/SemanticNode/) |  |
| `action` | [LocatorAction](/api/E2E.Engine/LocatorAction/) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_PerformAsync_E2E_Engine_SemanticNode_E2E_Engine_LocatorAction_System_Threading_CancellationToken_"></a>

### PerformAsync(SemanticNode, LocatorAction, CancellationToken)


```csharp
Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `node` | [SemanticNode](/api/E2E.Engine/SemanticNode/) |  |
| `action` | [LocatorAction](/api/E2E.Engine/LocatorAction/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_PerformAtAsync_"></a>
<a id="E2E_Engine_IEngineSession_PerformAtAsync_E2E_Engine_Point_E2E_Engine_PointerAction_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### PerformAtAsync(Point, PointerAction, OperationContext, CancellationToken)


```csharp
Task PerformAtAsync(Point point, PointerAction action, OperationContext context, CancellationToken cancellationToken)
```


`PerformAtAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's
`performAt(point, action, operation)`. The default drops the context, as `PerformAsync` does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `action` | [PointerAction](/api/E2E.Engine/PointerAction/) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_PerformAtAsync_E2E_Engine_Point_E2E_Engine_PointerAction_System_Threading_CancellationToken_"></a>

### PerformAtAsync(Point, PointerAction, CancellationToken)


```csharp
Task PerformAtAsync(Point point, PointerAction action, CancellationToken cancellationToken)
```


Carries out one pointer action at a viewport point, with no node behind it. Engines that declare pointer actions implement it.
Upstream's `performAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `action` | [PointerAction](/api/E2E.Engine/PointerAction/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_PressAsync_"></a>
<a id="E2E_Engine_IEngineSession_PressAsync_System_String_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### PressAsync(string, OperationContext, CancellationToken)


```csharp
Task PressAsync(string key, OperationContext context, CancellationToken cancellationToken)
```


`PressAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `keyboard.press(key, operation)`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_PressAsync_System_String_System_Threading_CancellationToken_"></a>

### PressAsync(string, CancellationToken)


```csharp
Task PressAsync(string key, CancellationToken cancellationToken)
```


Sends a key to whatever holds focus. The harness has already validated the key.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_ResizeAsync_"></a>
<a id="E2E_Engine_IEngineSession_ResizeAsync_E2E_Engine_TraceViewport_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ResizeAsync(TraceViewport, OperationContext, CancellationToken)


```csharp
Task ResizeAsync(TraceViewport size, OperationContext context, CancellationToken cancellationToken)
```


Sets the viewport to &lt;code class="paramref"&gt;size&lt;/code&gt; in CSS pixels, for this page and every page the attempt opens after it. Engines that declare
[Resize](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Resize) implement it. Upstream's `session.resize`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `size` | [TraceViewport](/api/E2E.Engine/TraceViewport/) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_RestartAsync_"></a>
<a id="E2E_Engine_IEngineSession_RestartAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### RestartAsync(OperationContext, CancellationToken)


```csharp
Task RestartAsync(OperationContext context, CancellationToken cancellationToken)
```


[RestartAsync(CancellationToken)](#E2E_Engine_IEngineSession_RestartAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `session.restart`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_RestartAsync_System_Threading_CancellationToken_"></a>

### RestartAsync(CancellationToken)


```csharp
Task RestartAsync(CancellationToken cancellationToken)
```


Closes the current document and opens a blank one, keeping cookies and storage.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_RestoreStateAsync_"></a>
<a id="E2E_Engine_IEngineSession_RestoreStateAsync_E2E_EngineState_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### RestoreStateAsync(EngineState, OperationContext, CancellationToken)


```csharp
Task RestoreStateAsync(EngineState state, OperationContext context, CancellationToken cancellationToken)
```


`RestoreStateAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `state.restore`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `state` | [EngineState](/api/E2E/EngineState/) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_RestoreStateAsync_E2E_EngineState_System_Threading_CancellationToken_"></a>

### RestoreStateAsync(EngineState, CancellationToken)


```csharp
Task RestoreStateAsync(EngineState state, CancellationToken cancellationToken)
```


Replaces the app's current state with one [CaptureStateAsync(CancellationToken)](#E2E_Engine_IEngineSession_CaptureStateAsync_System_Threading_CancellationToken_) captured. Upstream's optional `restoreState`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `state` | [EngineState](/api/E2E/EngineState/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_ScreenshotArtifactAsync_"></a>
<a id="E2E_Engine_IEngineSession_ScreenshotArtifactAsync_System_String_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ScreenshotArtifactAsync(string?, OperationContext, CancellationToken)


```csharp
Task<string> ScreenshotArtifactAsync(string? label, OperationContext context, CancellationToken cancellationToken)
```


`ScreenshotArtifactAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `artifacts.screenshot`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotArtifactAsync_System_String_System_Threading_CancellationToken_"></a>

### ScreenshotArtifactAsync(string?, CancellationToken)


```csharp
Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
```


Captures a redacted screenshot into the attempt's artifact directory and returns its artifact-relative path, `screenshots/NNN-label.png`.
Every secure field is covered before the file is written. Upstream's `session.artifacts.screenshot`; an engine that keeps none
fails with `UNSUPPORTED_CAPABILITY`, as upstream's guard does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotArtifactAsync_System_String_System_TimeSpan_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ScreenshotArtifactAsync(string?, TimeSpan, OperationContext, CancellationToken)


```csharp
Task<string> ScreenshotArtifactAsync(string? label, TimeSpan timeout, OperationContext context, CancellationToken cancellationToken)
```


`ScreenshotArtifactAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `artifacts.screenshot`. The default passes the timeout on as the context's timeout (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotArtifactAsync_System_String_System_TimeSpan_System_Threading_CancellationToken_"></a>

### ScreenshotArtifactAsync(string?, TimeSpan, CancellationToken)


```csharp
Task<string> ScreenshotArtifactAsync(string? label, TimeSpan timeout, CancellationToken cancellationToken)
```


`ScreenshotArtifactAsync` within &lt;code class="paramref"&gt;timeout&lt;/code&gt;, the operation budget its snapshot
and capture share (upstream's `artifacts.screenshot`). The default passes it on as the call's timeout (.NET-only, as
`ObserveAsync`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotAsync_"></a>
<a id="E2E_Engine_IEngineSession_ScreenshotAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ScreenshotAsync(OperationContext, CancellationToken)


```csharp
Task<EngineScreenshot> ScreenshotAsync(OperationContext context, CancellationToken cancellationToken)
```


[ScreenshotAsync(CancellationToken)](#E2E_Engine_IEngineSession_ScreenshotAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `artifacts.screenshot`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EngineScreenshot&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotAsync_System_Threading_CancellationToken_"></a>

### ScreenshotAsync(CancellationToken)


```csharp
Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken)
```


Captures the viewport as a PNG for `toHaveScreenshot`, with animations stopped, the text caret hidden,
and every secure field covered before the image leaves the engine. Engines that declare
[Screenshot](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Screenshot) implement it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EngineScreenshot&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotAsync_System_TimeSpan_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### ScreenshotAsync(TimeSpan, OperationContext, CancellationToken)


```csharp
Task<EngineScreenshot> ScreenshotAsync(TimeSpan timeout, OperationContext context, CancellationToken cancellationToken)
```


`ScreenshotAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `artifacts.screenshot` with its `operation.timeoutMs`. The default passes the timeout on as the context's timeout (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EngineScreenshot&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_ScreenshotAsync_System_TimeSpan_System_Threading_CancellationToken_"></a>

### ScreenshotAsync(TimeSpan, CancellationToken)


```csharp
Task<EngineScreenshot> ScreenshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
```


[ScreenshotAsync(CancellationToken)](#E2E_Engine_IEngineSession_ScreenshotAsync_System_Threading_CancellationToken_) within &lt;code class="paramref"&gt;timeout&lt;/code&gt;, the operation budget left for the capture
(upstream's `operation.timeoutMs`). An engine that bounds its reads overrides it and ends the capture at the budget with
`OPERATION_TIMEOUT`; the default passes it on as the call's timeout (.NET-only, as `ObserveAsync`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EngineScreenshot&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_SettleAttemptAsync_"></a>
<a id="E2E_Engine_IEngineSession_SettleAttemptAsync_System_TimeSpan_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### SettleAttemptAsync(TimeSpan, OperationContext, CancellationToken)


```csharp
Task SettleAttemptAsync(TimeSpan timeout, OperationContext context, CancellationToken cancellationToken)
```


`SettleAttemptAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `settle(operation)`, whose `operation.timeoutMs` is the cleanup budget. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_SettleAttemptAsync_System_TimeSpan_System_Threading_CancellationToken_"></a>

### SettleAttemptAsync(TimeSpan, CancellationToken)


```csharp
Task SettleAttemptAsync(TimeSpan timeout, CancellationToken cancellationToken)
```


After the body has settled, and again after teardown, before the verdict, within the cleanup budget: report a failure that landed
on a path no step awaited (a callback the session ran for the test: a request interceptor, a native dialog handler). Wait for one
still running, then throw its error; the attempt fails with it the way a step would, an error already classified keeping its code.
MUST report each failure once. Upstream's `settleAttempt`, whose `EngineCleanupContext` is the timeout and the token.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_StartVideoAsync_"></a>
<a id="E2E_Engine_IEngineSession_StartVideoAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### StartVideoAsync(OperationContext, CancellationToken)


```csharp
Task StartVideoAsync(OperationContext context, CancellationToken cancellationToken)
```


[StartVideoAsync(CancellationToken)](#E2E_Engine_IEngineSession_StartVideoAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `artifacts.startVideo`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_StartVideoAsync_System_Threading_CancellationToken_"></a>

### StartVideoAsync(CancellationToken)


```csharp
Task StartVideoAsync(CancellationToken cancellationToken)
```


Starts recording the surface, once its page can be recorded. Engines that declare
[Video](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Video) implement it. An attempt may start and stop more than once, never overlapping: the
runner calls it for the attempt's video and for each `app.StartRecordingAsync()`, and moves the files a stop returns
before the next start. Upstream's `startVideo`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_StopVideoAsync_"></a>
<a id="E2E_Engine_IEngineSession_StopVideoAsync_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### StopVideoAsync(OperationContext, CancellationToken)


```csharp
Task<IReadOnlyList<VideoSegment>> StopVideoAsync(OperationContext context, CancellationToken cancellationToken)
```


[StopVideoAsync(CancellationToken)](#E2E_Engine_IEngineSession_StopVideoAsync_System_Threading_CancellationToken_) with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `artifacts.stopVideo`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;VideoSegment&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_StopVideoAsync_System_Threading_CancellationToken_"></a>

### StopVideoAsync(CancellationToken)


```csharp
Task<IReadOnlyList<VideoSegment>> StopVideoAsync(CancellationToken cancellationToken)
```


Stops the recording and returns the segments written, in order: files in the attempt's `video` directory, or
links to recordings a hosted service keeps. Engines that declare [Video](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Video) implement
it. Upstream's `stopVideo`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;VideoSegment&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IEngineSession_SwipeAsync_"></a>
<a id="E2E_Engine_IEngineSession_SwipeAsync_E2E_Engine_ScrollDirection_System_Nullable_E2E_Engine_Momentum__E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### SwipeAsync(ScrollDirection, Momentum?, OperationContext, CancellationToken)


```csharp
Task SwipeAsync(ScrollDirection direction, Momentum? momentum, OperationContext context, CancellationToken cancellationToken)
```


`SwipeAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream swipes the screen root
through `perform(root, swipe, operation)`. The default drops the context, as `PerformAsync` does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `momentum` | `Momentum?` |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_SwipeAsync_E2E_Engine_ScrollDirection_System_Nullable_E2E_Engine_Momentum__System_Threading_CancellationToken_"></a>

### SwipeAsync(ScrollDirection, Momentum?, CancellationToken)


```csharp
Task SwipeAsync(ScrollDirection direction, Momentum? momentum, CancellationToken cancellationToken)
```


`SwipeAsync` with the stride of the gesture: upstream's `swipe(direction,
momentum)`. An engine that takes a momentum overrides it; the default drops it, so an engine with one stride swipes by that.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `momentum` | `Momentum?` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_SwipeAsync_E2E_Engine_ScrollDirection_System_Threading_CancellationToken_"></a>

### SwipeAsync(ScrollDirection, CancellationToken)


```csharp
Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken)
```


Scrolls the viewport about three quarters of a screen. Engines that declare
[Scroll](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Scroll) implement it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_SwitchTabAsync_"></a>
<a id="E2E_Engine_IEngineSession_SwitchTabAsync_System_Int32_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### SwitchTabAsync(int, OperationContext, CancellationToken)


```csharp
Task SwitchTabAsync(int index, OperationContext context, CancellationToken cancellationToken)
```


Shows the open tab at &lt;code class="paramref"&gt;index&lt;/code&gt;, zero-based in the order the tabs opened, as [Tabs](/api/E2E.Engine/Observation/#E2E_Engine_Observation_Tabs) counts them:
every observation and action after it reads and acts on that tab. An index with no open tab is an `ACTION_FAILED` the model
reads. Engines that declare [SwitchTab](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_SwitchTab) implement it. Upstream's `session.switchTab`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_TypeTextAsync_"></a>
<a id="E2E_Engine_IEngineSession_TypeTextAsync_System_String_System_Boolean_E2E_Engine_OperationContext_System_Threading_CancellationToken_"></a>

### TypeTextAsync(string, bool, OperationContext, CancellationToken)


```csharp
Task TypeTextAsync(string text, bool replace, OperationContext context, CancellationToken cancellationToken)
```


`TypeTextAsync` with &lt;code class="paramref"&gt;context&lt;/code&gt;: upstream's `keyboard.type`. The default drops the context (.NET-only).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `replace` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `context` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IEngineSession_TypeTextAsync_System_String_System_Boolean_System_Threading_CancellationToken_"></a>

### TypeTextAsync(string, bool, CancellationToken)


```csharp
Task TypeTextAsync(string text, bool replace, CancellationToken cancellationToken)
```


Types &lt;code class="paramref"&gt;text&lt;/code&gt; into whatever holds focus, with no node resolved: how a field the tree does not list (drawn on a
canvas, flattened out of a platform's tree) gets its text after a tap gave it focus. With &lt;code class="paramref"&gt;replace&lt;/code&gt; the field's
value is cleared first, select all and delete. An engine that finds nothing that takes text focused refuses with
`NOT_ACTIONABLE`. Engines that declare [Keyboard](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Keyboard) implement it; the default refuses with
`UNSUPPORTED_CAPABILITY` (.NET-only: it keeps an engine written before the member compiling). Upstream's `keyboard.type`,
with `PressAsync` its `keyboard.press`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `replace` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
