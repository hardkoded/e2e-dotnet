---
title: "LocatorExpect"
description: "Matchers that retry until they pass or the assertion timeout elapses. Each takes an optional timeout that replaces the assertion timeout for that call."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorExpect"
  hidden: true
---

<a id="E2E_LocatorExpect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class LocatorExpect
```


Matchers that retry until they pass or the assertion timeout elapses. Each takes an optional
`timeout` that replaces the assertion timeout for that call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L88)

## Properties

<a id="E2E_LocatorExpect_Not_"></a>
<a id="E2E_LocatorExpect_Not"></a>

### Not


```csharp
public LocatorExpect Not { get; }
```


Inverts the matcher. A negated matcher passes after its condition has been false for 1000 ms in a row.

**Value:** [LocatorExpect](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L100)

## Methods

<a id="E2E_LocatorExpect_ToBeAttachedAsync_"></a>
<a id="E2E_LocatorExpect_ToBeAttachedAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeAttachedAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeAttachedAsync(bool attached = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for one match to exist, visible or not. &lt;code class="paramref"&gt;attached&lt;/code&gt; false waits for none.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `attached` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L125)

<a id="E2E_LocatorExpect_ToBeCheckedAsync_"></a>
<a id="E2E_LocatorExpect_ToBeCheckedAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeCheckedAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeCheckedAsync(bool isChecked = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for checked state. &lt;code class="paramref"&gt;isChecked&lt;/code&gt; false waits for unchecked state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `isChecked` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L151)

<a id="E2E_LocatorExpect_ToBeDisabledAsync_"></a>
<a id="E2E_LocatorExpect_ToBeDisabledAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeDisabledAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeDisabledAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for disabled state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L145)

<a id="E2E_LocatorExpect_ToBeEnabledAsync_"></a>
<a id="E2E_LocatorExpect_ToBeEnabledAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeEnabledAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeEnabledAsync(bool enabled = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for enabled state. &lt;code class="paramref"&gt;enabled&lt;/code&gt; false waits for disabled state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `enabled` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L139)

<a id="E2E_LocatorExpect_ToBeExpandedAsync_"></a>
<a id="E2E_LocatorExpect_ToBeExpandedAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeExpandedAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeExpandedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for expanded state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L163)

<a id="E2E_LocatorExpect_ToBeFocusedAsync_"></a>
<a id="E2E_LocatorExpect_ToBeFocusedAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeFocusedAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeFocusedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for focused state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L169)

<a id="E2E_LocatorExpect_ToBeHiddenAsync_"></a>
<a id="E2E_LocatorExpect_ToBeHiddenAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeHiddenAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeHiddenAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for hidden or absent state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L119)

<a id="E2E_LocatorExpect_ToBeSelectedAsync_"></a>
<a id="E2E_LocatorExpect_ToBeSelectedAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeSelectedAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeSelectedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for selected state.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L157)

<a id="E2E_LocatorExpect_ToBeVisibleAsync_"></a>
<a id="E2E_LocatorExpect_ToBeVisibleAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeVisibleAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeVisibleAsync(bool visible = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for one visible match. &lt;code class="paramref"&gt;visible&lt;/code&gt; false waits for hidden or absent, as `CancellationToken)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `visible` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L113)

<a id="E2E_LocatorExpect_ToContainTextAsync_"></a>
<a id="E2E_LocatorExpect_ToContainTextAsync_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToContainTextAsync(TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToContainTextAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for contained normalized text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L190)

<a id="E2E_LocatorExpect_ToContainTextAsync_System_Collections_Generic_IReadOnlyList_E2E_TextMatch__System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### `ToContainTextAsync(IReadOnlyList<TextMatch>, bool?, TimeSpan?, CancellationToken)`


```csharp
public Task ToContainTextAsync(IReadOnlyList<TextMatch> expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for each entry to be contained by a distinct match, in order. Extra matches are allowed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [IReadOnlyList&lt;TextMatch&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L196)

<a id="E2E_LocatorExpect_ToHaveAccessibleNameAsync_"></a>
<a id="E2E_LocatorExpect_ToHaveAccessibleNameAsync_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveAccessibleNameAsync(TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveAccessibleNameAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for an accessible name.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L208)

<a id="E2E_LocatorExpect_ToHaveAttributeAsync_"></a>
<a id="E2E_LocatorExpect_ToHaveAttributeAsync_System_String_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveAttributeAsync(string, TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveAttributeAsync(string name, TextMatch value, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for the attribute to be present and its normalized value to match.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `value` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L221)

<a id="E2E_LocatorExpect_ToHaveAttributeAsync_System_String_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveAttributeAsync(string, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveAttributeAsync(string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for the attribute to be present.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L214)

<a id="E2E_LocatorExpect_ToHaveCountAsync_"></a>
<a id="E2E_LocatorExpect_ToHaveCountAsync_System_Int32_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveCountAsync(int, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveCountAsync(int count, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for an exact match count.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `count` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L229)

<a id="E2E_LocatorExpect_ToHaveScreenshotAsync_"></a>
<a id="E2E_LocatorExpect_ToHaveScreenshotAsync_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(ScreenshotOptions, string?)


```csharp
public Task ToHaveScreenshotAsync(ScreenshotOptions options, string? callerFile = null)
```


Waits for the one match to look like its stored screenshot, the box of the node cut from the screen. See `String)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L109)

<a id="E2E_LocatorExpect_ToHaveScreenshotAsync_System_String_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(string?, ScreenshotOptions?, string?)


```csharp
public Task ToHaveScreenshotAsync(string? name = null, ScreenshotOptions? options = null, string? callerFile = null)
```


Waits for the one match to look like its stored screenshot, the box of the node cut from the screen. See `String)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L105)

<a id="E2E_LocatorExpect_ToHaveTextAsync_"></a>
<a id="E2E_LocatorExpect_ToHaveTextAsync_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveTextAsync(TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveTextAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for exact normalized text: the node's rendered text (`innerText` on the web), not its label.
&lt;code class="paramref"&gt;ignoreCase&lt;/code&gt; folds a string's case, and adds or removes a pattern's.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L178)

<a id="E2E_LocatorExpect_ToHaveTextAsync_System_Collections_Generic_IReadOnlyList_E2E_TextMatch__System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### `ToHaveTextAsync(IReadOnlyList<TextMatch>, bool?, TimeSpan?, CancellationToken)`


```csharp
public Task ToHaveTextAsync(IReadOnlyList<TextMatch> expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for exactly as many matches as entries, each with its entry's text, in order.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [IReadOnlyList&lt;TextMatch&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L184)

<a id="E2E_LocatorExpect_ToHaveValueAsync_"></a>
<a id="E2E_LocatorExpect_ToHaveValueAsync_E2E_TextMatch_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveValueAsync(TextMatch, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveValueAsync(TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for a form control's value, compared as it is. Fails on a node that is not a form control, negated too.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L202)
