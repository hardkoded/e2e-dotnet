---
title: "SoftLocatorExpect"
description: "The locator matchers of LocatorExpect, but an ASSERTIONFAILED is kept on the session instead of thrown, and the test body runs on. Any other error, such as S..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SoftLocatorExpect"
  hidden: true
---

<a id="E2E_SoftLocatorExpect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SoftLocatorExpect
```


The locator matchers of [LocatorExpect](/api/E2E/LocatorExpect/), but an
`ASSERTION_FAILED` is kept on the session instead of thrown, and the
test body runs on. Any other error, such as `STRICT_MODE`, still throws.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L16)

## Properties

<a id="E2E_SoftLocatorExpect_Not_"></a>
<a id="E2E_SoftLocatorExpect_Not"></a>

### Not


```csharp
public SoftLocatorExpect Not { get; }
```


Inverts the matcher, as [Not](/api/E2E/LocatorExpect/#E2E_LocatorExpect_Not). A failure is still kept, not thrown.

**Value:** [SoftLocatorExpect](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L28)

## Methods

<a id="E2E_SoftLocatorExpect_ToBeAttachedAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeAttachedAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeAttachedAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeAttachedAsync(bool attached = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `attached` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L34)

<a id="E2E_SoftLocatorExpect_ToBeCheckedAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeCheckedAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeCheckedAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeCheckedAsync(bool isChecked = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `isChecked` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L40)

<a id="E2E_SoftLocatorExpect_ToBeDisabledAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeDisabledAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeDisabledAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeDisabledAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L38)

<a id="E2E_SoftLocatorExpect_ToBeEnabledAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeEnabledAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeEnabledAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeEnabledAsync(bool enabled = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `enabled` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L36)

<a id="E2E_SoftLocatorExpect_ToBeExpandedAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeExpandedAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeExpandedAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeExpandedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L44)

<a id="E2E_SoftLocatorExpect_ToBeFocusedAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeFocusedAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeFocusedAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeFocusedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L46)

<a id="E2E_SoftLocatorExpect_ToBeHiddenAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeHiddenAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeHiddenAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeHiddenAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L32)

<a id="E2E_SoftLocatorExpect_ToBeSelectedAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeSelectedAsync_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeSelectedAsync(TimeSpan?, CancellationToken)


```csharp
public Task ToBeSelectedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L42)

<a id="E2E_SoftLocatorExpect_ToBeVisibleAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToBeVisibleAsync_System_Boolean_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToBeVisibleAsync(bool, TimeSpan?, CancellationToken)


```csharp
public Task ToBeVisibleAsync(bool visible = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `visible` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L30)

<a id="E2E_SoftLocatorExpect_ToContainTextAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToContainTextAsync_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToContainTextAsync(TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToContainTextAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L52)

<a id="E2E_SoftLocatorExpect_ToContainTextAsync_System_Collections_Generic_IReadOnlyList_E2E_TextMatch__System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### `ToContainTextAsync(IReadOnlyList<TextMatch>, bool?, TimeSpan?, CancellationToken)`


```csharp
public Task ToContainTextAsync(IReadOnlyList<TextMatch> expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [IReadOnlyList&lt;TextMatch&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L54)

<a id="E2E_SoftLocatorExpect_ToHaveAccessibleNameAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToHaveAccessibleNameAsync_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveAccessibleNameAsync(TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveAccessibleNameAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L58)

<a id="E2E_SoftLocatorExpect_ToHaveAttributeAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToHaveAttributeAsync_System_String_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveAttributeAsync(string, TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveAttributeAsync(string name, TextMatch value, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `value` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L62)

<a id="E2E_SoftLocatorExpect_ToHaveAttributeAsync_System_String_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveAttributeAsync(string, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveAttributeAsync(string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L60)

<a id="E2E_SoftLocatorExpect_ToHaveCountAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToHaveCountAsync_System_Int32_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveCountAsync(int, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveCountAsync(int count, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `count` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L68)

<a id="E2E_SoftLocatorExpect_ToHaveScreenshotAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToHaveScreenshotAsync_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(ScreenshotOptions, string?)


```csharp
public Task ToHaveScreenshotAsync(ScreenshotOptions options, string? callerFile = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L66)

<a id="E2E_SoftLocatorExpect_ToHaveScreenshotAsync_System_String_E2E_ScreenshotOptions_System_String_"></a>

### ToHaveScreenshotAsync(string?, ScreenshotOptions?, string?)


```csharp
public Task ToHaveScreenshotAsync(string? name = null, ScreenshotOptions? options = null, string? callerFile = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ScreenshotOptions](/api/E2E/ScreenshotOptions/) |  |
| `callerFile` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L64)

<a id="E2E_SoftLocatorExpect_ToHaveTextAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToHaveTextAsync_E2E_TextMatch_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveTextAsync(TextMatch, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveTextAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L48)

<a id="E2E_SoftLocatorExpect_ToHaveTextAsync_System_Collections_Generic_IReadOnlyList_E2E_TextMatch__System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### `ToHaveTextAsync(IReadOnlyList<TextMatch>, bool?, TimeSpan?, CancellationToken)`


```csharp
public Task ToHaveTextAsync(IReadOnlyList<TextMatch> expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [IReadOnlyList&lt;TextMatch&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L50)

<a id="E2E_SoftLocatorExpect_ToHaveValueAsync_"></a>
<a id="E2E_SoftLocatorExpect_ToHaveValueAsync_E2E_TextMatch_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveValueAsync(TextMatch, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveValueAsync(TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L56)
