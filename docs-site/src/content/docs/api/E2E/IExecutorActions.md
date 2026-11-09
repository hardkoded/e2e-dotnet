---
title: "IExecutorActions"
description: "Upstream's ExecutorActions, with the verbs the port's engines support. Every verb bottoms out in the runtime, which enforces the deadline and the action budg..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IExecutorActions"
  hidden: true
---

<a id="E2E_IExecutorActions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IExecutorActions
```


Upstream's `ExecutorActions`, with the verbs the port's engines support. Every verb bottoms out in the runtime, which
enforces the deadline and the action budget and records the action for the step's cache entry. A ref is only valid against
the newest observation: a stale one fails the action rather than acting on the wrong node. Actions and observations run
in call order. An engine failure is thrown as an [E2EException](/api/E2E/E2EException/), and a hard stop of the step as an
[AgentException](/api/E2E/AgentException/) with a runtime code. `ClearAsync` is a verb the port's grammar has and
upstream's does not (upstream clears with `type`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Methods

<a id="E2E_IExecutorActions_BackAsync_"></a>
<a id="E2E_IExecutorActions_BackAsync_System_Threading_CancellationToken_"></a>

### BackAsync(CancellationToken)


```csharp
Task BackAsync(CancellationToken cancellationToken = default)
```


Navigates back once.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_CheckAsync_"></a>
<a id="E2E_IExecutorActions_CheckAsync_E2E_ExecutorTarget_System_Boolean_System_Threading_CancellationToken_"></a>

### CheckAsync(ExecutorTarget, bool, CancellationToken)


```csharp
Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default)
```


Sets a checkbox, switch, or radio to &lt;code class="paramref"&gt;check&lt;/code&gt;. A control already in that state is left alone, which
is what tells this verb from a tap that flips whatever state it finds.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `check` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_ClearAsync_"></a>
<a id="E2E_IExecutorActions_ClearAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### ClearAsync(ExecutorTarget, CancellationToken)


```csharp
Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_DismissKeyboardAsync_"></a>
<a id="E2E_IExecutorActions_DismissKeyboardAsync_System_Threading_CancellationToken_"></a>

### DismissKeyboardAsync(CancellationToken)


```csharp
Task DismissKeyboardAsync(CancellationToken cancellationToken = default)
```


Hides an on-screen keyboard, so a control it covered can be reached. Needs [DismissKeyboard](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_DismissKeyboard) in
[Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs). The default refuses with `UNSUPPORTED_CAPABILITY` (.NET-only: it keeps an executor
context written before the member compiling; the harness's own actions override it). Upstream's `dismissKeyboard`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_DoubleTapAsync_"></a>
<a id="E2E_IExecutorActions_DoubleTapAsync_E2E_ExecutorTarget_E2E_ClickOptions_System_Threading_CancellationToken_"></a>

### DoubleTapAsync(ExecutorTarget, ClickOptions?, CancellationToken)


```csharp
Task DoubleTapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default)
```


Double-taps one node. Takes the tap's &lt;code class="paramref"&gt;options&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `options` | [ClickOptions](/api/E2E/ClickOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_DoubleTapAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### DoubleTapAsync(ExecutorTarget, CancellationToken)


```csharp
Task DoubleTapAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


Double-taps one node, holding no keys.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_DragAsync_"></a>
<a id="E2E_IExecutorActions_DragAsync_E2E_ExecutorTarget_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### DragAsync(ExecutorTarget, ExecutorTarget, CancellationToken)


```csharp
Task DragAsync(ExecutorTarget source, ExecutorTarget destination, CancellationToken cancellationToken = default)
```


Drags one node onto another. Both are named by their ids from the newest observation and found on one look, so the drag
never joins a source from one screen to a destination from another. Needs [Drag](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_Drag) in
[Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `source` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `destination` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_DragAtAsync_"></a>
<a id="E2E_IExecutorActions_DragAtAsync_E2E_Engine_Point_E2E_Engine_Point_System_Threading_CancellationToken_"></a>

### DragAtAsync(Point, Point, CancellationToken)


```csharp
Task<PointDragResult> DragAtAsync(Point from, Point to, CancellationToken cancellationToken = default)
```


Drags from one viewport point to another, in the CSS pixels `TapAtAsync` takes, for a drag the tree does not list: a
shape on a canvas, a pin on a map, a card dropped on an empty spot. The engine gets the bare points through its `dragTo`
pointer kind ([DragAt](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_DragAt)), recorded with the viewport and each point's place inside the node under it. An
engine without it joins the two different enabled controls holding the ends, like a drag by id, and fails when the points do not
land on two such controls. Upstream's `dragAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `from` | [Point](/api/E2E.Engine/Point/) |  |
| `to` | [Point](/api/E2E.Engine/Point/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;PointDragResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_IExecutorActions_HitTestAsync_"></a>
<a id="E2E_IExecutorActions_HitTestAsync_E2E_Engine_Point_System_Threading_CancellationToken_"></a>

### HitTestAsync(Point, CancellationToken)


```csharp
Task<PointHit> HitTestAsync(Point point, CancellationToken cancellationToken = default)
```


What the newest observation lists at one viewport point, in the same CSS pixels `TapAtAsync` takes: the innermost
enabled control whose box contains the point, and the innermost listed node of any role. What a point-addressed verb resolves its
target through before calling `TypeAsync`. Costs no action: the verb the executor calls with the result does.
Upstream's `hitTest`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;PointHit&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_IExecutorActions_HoverAsync_"></a>
<a id="E2E_IExecutorActions_HoverAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### HoverAsync(ExecutorTarget, CancellationToken)


```csharp
Task HoverAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


Moves the pointer over one node, as a hover that reveals a menu or a tooltip. Needs [Hover](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_Hover) in [Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_HoverAtAsync_"></a>
<a id="E2E_IExecutorActions_HoverAtAsync_E2E_Engine_Point_System_Threading_CancellationToken_"></a>

### HoverAtAsync(Point, CancellationToken)


```csharp
Task<PointTapResult> HoverAtAsync(Point point, CancellationToken cancellationToken = default)
```


Hovers one viewport point the way `TapAtAsync` taps one: the bare point through the engine's `hover` pointer kind
([HoverAt](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_HoverAt)). Upstream's `hoverAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;PointTapResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_IExecutorActions_LongPressAsync_"></a>
<a id="E2E_IExecutorActions_LongPressAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### LongPressAsync(ExecutorTarget, CancellationToken)


```csharp
Task LongPressAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


Holds one node pressed, as a touch long press does. Needs [LongPress](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_LongPress) in [Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_NavigateAsync_"></a>
<a id="E2E_IExecutorActions_NavigateAsync_System_String_System_Threading_CancellationToken_"></a>

### NavigateAsync(string, CancellationToken)


```csharp
Task NavigateAsync(string url, CancellationToken cancellationToken = default)
```


Navigates to an http(s) URL or an app-relative path.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_PressAsync_"></a>
<a id="E2E_IExecutorActions_PressAsync_E2E_ExecutorTarget_System_String_System_Threading_CancellationToken_"></a>

### PressAsync(ExecutorTarget?, string, CancellationToken)


```csharp
Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default)
```


Presses &lt;code class="paramref"&gt;key&lt;/code&gt; on &lt;code class="paramref"&gt;target&lt;/code&gt;, or on the focused control when there is none, which is
`PressKeyAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_PressKeyAsync_"></a>
<a id="E2E_IExecutorActions_PressKeyAsync_System_String_System_Threading_CancellationToken_"></a>

### PressKeyAsync(string, CancellationToken)


```csharp
Task PressKeyAsync(string key, CancellationToken cancellationToken = default)
```


Sends one key to whatever holds focus, with no node resolved. Needs [PressKey](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_PressKey) in
[Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs). Upstream's `pressKey`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_ResizeAsync_"></a>
<a id="E2E_IExecutorActions_ResizeAsync_E2E_Engine_TraceViewport_System_Threading_CancellationToken_"></a>

### ResizeAsync(TraceViewport, CancellationToken)


```csharp
Task ResizeAsync(TraceViewport size, CancellationToken cancellationToken = default)
```


Sets the viewport to a size in whole CSS pixels, 200 to 4096 on each side, for this page and every page the attempt opens after
it. A size outside the limits is `INVALID_ARGUMENT`. Needs [Resize](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_Resize) in
[Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs). The default refuses with `UNSUPPORTED_CAPABILITY` (.NET-only: it keeps an executor
context written before the member compiling; the harness's own actions override it). Upstream's `resize`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `size` | [TraceViewport](/api/E2E.Engine/TraceViewport/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_ScrollAsync_"></a>
<a id="E2E_IExecutorActions_ScrollAsync_E2E_ExecutorTarget_E2E_Engine_ScrollDirection_System_Int32_System_Threading_CancellationToken_"></a>

### ScrollAsync(ExecutorTarget?, ScrollDirection, int, CancellationToken)


```csharp
Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times = 1, CancellationToken cancellationToken = default)
```


Scrolls &lt;code class="paramref"&gt;list&lt;/code&gt;, or the viewport when there is none, &lt;code class="paramref"&gt;times&lt;/code&gt; times.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `list` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `times` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_ScrollToAsync_"></a>
<a id="E2E_IExecutorActions_ScrollToAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### ScrollToAsync(ExecutorTarget, CancellationToken)


```csharp
Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


Brings one listed node into the viewport.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_ScrollUntilAsync_"></a>
<a id="E2E_IExecutorActions_ScrollUntilAsync_System_String_E2E_Engine_ScrollDirection_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### ScrollUntilAsync(string, ScrollDirection, ExecutorTarget?, CancellationToken)


```csharp
Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list = null, CancellationToken cancellationToken = default)
```


Pages &lt;code class="paramref"&gt;list&lt;/code&gt;, or the viewport when there is none, screen by screen until a visible node reading
&lt;code class="paramref"&gt;text&lt;/code&gt; is inside the viewport. One action however many screens it takes, up to eight hundred. It
gives up as `LOCATOR_NOT_FOUND` when the screen stops moving or the cap is reached.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `direction` | [ScrollDirection](/api/E2E.Engine/ScrollDirection/) |  |
| `list` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_SecondaryTapAsync_"></a>
<a id="E2E_IExecutorActions_SecondaryTapAsync_E2E_ExecutorTarget_E2E_ClickOptions_System_Threading_CancellationToken_"></a>

### SecondaryTapAsync(ExecutorTarget, ClickOptions?, CancellationToken)


```csharp
Task SecondaryTapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default)
```


Taps one node with the secondary button, a right-click. Takes the tap's &lt;code class="paramref"&gt;options&lt;/code&gt;. Needs
[SecondaryTap](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_SecondaryTap) in [Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `options` | [ClickOptions](/api/E2E/ClickOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_SecondaryTapAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### SecondaryTapAsync(ExecutorTarget, CancellationToken)


```csharp
Task SecondaryTapAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


Right-clicks one node, holding no keys.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_SelectAsync_"></a>
<a id="E2E_IExecutorActions_SelectAsync_E2E_ExecutorTarget_System_String_System_Threading_CancellationToken_"></a>

### SelectAsync(ExecutorTarget, string, CancellationToken)


```csharp
Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_TapAsync_"></a>
<a id="E2E_IExecutorActions_TapAsync_E2E_ExecutorTarget_E2E_ClickOptions_System_Threading_CancellationToken_"></a>

### TapAsync(ExecutorTarget, ClickOptions?, CancellationToken)


```csharp
Task TapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default)
```


Taps one node. [Modifiers](/api/E2E/ClickOptions/#E2E_ClickOptions_Modifiers) holds keys for the click, as a Shift-click extends a selection; it
needs [TapModifiers](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_TapModifiers), `UNSUPPORTED_CAPABILITY` otherwise.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `options` | [ClickOptions](/api/E2E/ClickOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_TapAsync_E2E_ExecutorTarget_System_Threading_CancellationToken_"></a>

### TapAsync(ExecutorTarget, CancellationToken)


```csharp
Task TapAsync(ExecutorTarget target, CancellationToken cancellationToken = default)
```


Taps one node, holding no keys.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_TapAtAsync_"></a>
<a id="E2E_IExecutorActions_TapAtAsync_E2E_Engine_Point_System_Threading_CancellationToken_"></a>

### TapAtAsync(Point, CancellationToken)


```csharp
Task<PointTapResult> TapAtAsync(Point point, CancellationToken cancellationToken = default)
```


Taps one viewport point, in the CSS pixels of the newest observation ([Rect](/api/E2E.Engine/SemanticNode/#E2E_Engine_SemanticNode_Rect) space; a point read off
[ExecutorPixels](/api/E2E/ExecutorPixels/) is divided by its [Scale](/api/E2E/ExecutorPixels/#E2E_ExecutorPixels_Scale)). The engine gets the bare point when it takes the
`tap` pointer kind ([TapAt](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_TapAt) in [Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs)), where the model aimed; an
engine that taps listed nodes only gets the enabled control whose box contains the point, tapped by its id like
`TapAsync`, and fails when the point is on nothing listed. A point off the viewport
is clamped to its edge. Upstream's `tapAt`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `point` | [Point](/api/E2E.Engine/Point/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;PointTapResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_IExecutorActions_TypeAsync_"></a>
<a id="E2E_IExecutorActions_TypeAsync_E2E_ExecutorTarget_System_String_System_Threading_CancellationToken_"></a>

### TypeAsync(ExecutorTarget, string, CancellationToken)


```csharp
Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_TypeSecretAsync_"></a>
<a id="E2E_IExecutorActions_TypeSecretAsync_E2E_ExecutorTarget_System_String_System_Threading_CancellationToken_"></a>

### TypeSecretAsync(ExecutorTarget, string, CancellationToken)


```csharp
Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken = default)
```


Fills one secret declared in the step's params into an input. The runtime authorizes the fill (a secret the step
declared and the attempt knows, an enabled editable sink, and a password field for a password) and hands the
plaintext straight to the engine: it never passes through the executor or any model. A refusal is
`POLICY_DENIED`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `secretName` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_TypeTextAsync_"></a>
<a id="E2E_IExecutorActions_TypeTextAsync_System_String_E2E_TypeTextOptions_System_Threading_CancellationToken_"></a>

### TypeTextAsync(string, TypeTextOptions?, CancellationToken)


```csharp
Task TypeTextAsync(string value, TypeTextOptions? options = null, CancellationToken cancellationToken = default)
```


Keyboard input to whatever holds focus. No node is resolved, so none is recorded: the trace keeps the value and replays it as given
after the actions that gave the field focus. An engine that finds nothing that takes text focused refuses with
`NOT_ACTIONABLE`, which reaches the executor as an ordinary failed action to read and work around. Needs
[TypeText](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_TypeText) in [Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs). Upstream's `typeText`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [TypeTextOptions](/api/E2E/TypeTextOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_IExecutorActions_UploadAsync_"></a>
<a id="E2E_IExecutorActions_UploadAsync_E2E_ExecutorTarget_System_Collections_Generic_IReadOnlyList_System_String__System_Threading_CancellationToken_"></a>

### `UploadAsync(ExecutorTarget, IReadOnlyList<string>, CancellationToken)`


```csharp
Task UploadAsync(ExecutorTarget target, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
```


Attaches files to a file input, or to a control that opens a file chooser. Paths are taken relative to the project root and
authorized by the harness before the engine sees them: a file outside the project, under a hidden directory, or absent is refused
([Upload](/api/E2E/ExecutorVerb/#E2E_ExecutorVerb_Upload) in [Verbs](/api/E2E/ExecutorTargetInfo/#E2E_ExecutorTargetInfo_Verbs)). A path the policy refuses is `POLICY_DENIED`,
and a missing file or a malformed list is `INVALID_ARGUMENT`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [ExecutorTarget](/api/E2E/ExecutorTarget/) |  |
| `paths` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
