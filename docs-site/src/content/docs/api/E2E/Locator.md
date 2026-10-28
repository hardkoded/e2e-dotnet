---
title: "Locator"
description: "A query that resolves against a fresh observation every time it is used."
editUrl: false
prev: false
next: false
sidebar:
  label: "Locator"
  hidden: true
---

<a id="E2E_Locator"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Locator
```


A query that resolves against a fresh observation every time it is used.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L382)

## Methods

<a id="E2E_Locator_AllAsync_"></a>
<a id="E2E_Locator_AllAsync_System_Threading_CancellationToken_"></a>

### AllAsync(CancellationToken)


```csharp
public Task<IReadOnlyList<Locator>> AllAsync(CancellationToken cancellationToken = default)
```


One [Nth(int)](#E2E_Locator_Nth_System_Int32_) locator per current match, without waiting. Empty when nothing matches.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;Locator&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L640)

<a id="E2E_Locator_AllTextContentsAsync_"></a>
<a id="E2E_Locator_AllTextContentsAsync_System_Threading_CancellationToken_"></a>

### AllTextContentsAsync(CancellationToken)


```csharp
public Task<IReadOnlyList<string>> AllTextContentsAsync(CancellationToken cancellationToken = default)
```


Reads the whitespace-normalized rendered text of every current match, without waiting. Empty when nothing matches.
Fails with `POLICY_DENIED` when any match is a secure field.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L656)

<a id="E2E_Locator_BoundingBoxAsync_"></a>
<a id="E2E_Locator_BoundingBoxAsync_System_Threading_CancellationToken_"></a>

### BoundingBoxAsync(CancellationToken)


```csharp
public Task<BoundingBox?> BoundingBoxAsync(CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads its viewport-relative box once, or null when the node lays out no box
(`display: none` on it or an ancestor, or any `display: contents` element) or the engine does not report one.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BoundingBox&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L626)

<a id="E2E_Locator_CheckAsync_"></a>
<a id="E2E_Locator_CheckAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### CheckAsync(ActionOptions?, CancellationToken)


```csharp
public Task CheckAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L529)

<a id="E2E_Locator_ClearAsync_"></a>
<a id="E2E_Locator_ClearAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### ClearAsync(ActionOptions?, CancellationToken)


```csharp
public Task ClearAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L539)

<a id="E2E_Locator_ClickAsync_"></a>
<a id="E2E_Locator_ClickAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### ClickAsync(ActionOptions?, CancellationToken)


```csharp
public Task ClickAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


Alias of `CancellationToken)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L484)

<a id="E2E_Locator_CountAsync_"></a>
<a id="E2E_Locator_CountAsync_System_Threading_CancellationToken_"></a>

### CountAsync(CancellationToken)


```csharp
public Task<int> CountAsync(CancellationToken cancellationToken = default)
```


Counts current matches without waiting.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;int&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L633)

<a id="E2E_Locator_DblClickAsync_"></a>
<a id="E2E_Locator_DblClickAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### DblClickAsync(ActionOptions?, CancellationToken)


```csharp
public Task DblClickAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


Alias of `CancellationToken)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L492)

<a id="E2E_Locator_DoubleTapAsync_"></a>
<a id="E2E_Locator_DoubleTapAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### DoubleTapAsync(ActionOptions?, CancellationToken)


```csharp
public Task DoubleTapAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L486)

<a id="E2E_Locator_FillAsync_"></a>
<a id="E2E_Locator_FillAsync_E2E_Secret_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### FillAsync(Secret, ActionOptions?, CancellationToken)


```csharp
public Task FillAsync(Secret value, ActionOptions? options = null, CancellationToken cancellationToken = default)
```


Fills a secret. The engine marks the value sensitive and never reports it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [Secret](/api/E2E/Secret/) |  |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L501)

<a id="E2E_Locator_FillAsync_System_String_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### FillAsync(string, ActionOptions?, CancellationToken)


```csharp
public Task FillAsync(string value, ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L494)

<a id="E2E_Locator_Filter_"></a>
<a id="E2E_Locator_Filter_E2E_Locator_"></a>

### Filter(Locator)


```csharp
public Locator Filter(Locator has)
```


Keeps the matches with a descendant that &lt;code class="paramref"&gt;has&lt;/code&gt; matches. &lt;code class="paramref"&gt;has&lt;/code&gt; resolves inside each match.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `has` | [Locator](./) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L403)

<a id="E2E_Locator_Filter_E2E_TextMatch_"></a>

### Filter(TextMatch)


```csharp
public Locator Filter(TextMatch hasText)
```


Keeps the matches whose own or descendants' name, text, or value contains &lt;code class="paramref"&gt;hasText&lt;/code&gt;, ignoring case.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `hasText` | [TextMatch](/api/E2E/TextMatch/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L395)

<a id="E2E_Locator_First_"></a>
<a id="E2E_Locator_First"></a>

### First()


```csharp
public Locator First()
```


**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L429)

<a id="E2E_Locator_FocusAsync_"></a>
<a id="E2E_Locator_FocusAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### FocusAsync(ActionOptions?, CancellationToken)


```csharp
public Task FocusAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L551)

<a id="E2E_Locator_GetAttributeAsync_"></a>
<a id="E2E_Locator_GetAttributeAsync_System_String_System_Threading_CancellationToken_"></a>

### GetAttributeAsync(string, CancellationToken)


```csharp
public Task<string?> GetAttributeAsync(string name, CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads one platform attribute once, or null when the node does not have it. Fails with `POLICY_DENIED` on a secure field.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L583)

<a id="E2E_Locator_GetByDisplayValue_"></a>
<a id="E2E_Locator_GetByDisplayValue_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByDisplayValue(TextMatch, TextMatchOptions)


```csharp
public Locator GetByDisplayValue(TextMatch value, TextMatchOptions options)
```


Creates a displayed-value query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L476)

<a id="E2E_Locator_GetByDisplayValue_E2E_TextMatch_System_Boolean_"></a>

### GetByDisplayValue(TextMatch, bool)


```csharp
public Locator GetByDisplayValue(TextMatch value, bool exact = true)
```


Creates a displayed-value query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L473)

<a id="E2E_Locator_GetByLabel_"></a>
<a id="E2E_Locator_GetByLabel_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByLabel(TextMatch, TextMatchOptions)


```csharp
public Locator GetByLabel(TextMatch label, TextMatchOptions options)
```


Creates a label query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L458)

<a id="E2E_Locator_GetByLabel_E2E_TextMatch_System_Boolean_"></a>

### GetByLabel(TextMatch, bool)


```csharp
public Locator GetByLabel(TextMatch label, bool exact = true)
```


Creates a label query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L455)

<a id="E2E_Locator_GetByPlaceholder_"></a>
<a id="E2E_Locator_GetByPlaceholder_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByPlaceholder(TextMatch, TextMatchOptions)


```csharp
public Locator GetByPlaceholder(TextMatch placeholder, TextMatchOptions options)
```


Creates a placeholder query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `placeholder` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L470)

<a id="E2E_Locator_GetByPlaceholder_E2E_TextMatch_System_Boolean_"></a>

### GetByPlaceholder(TextMatch, bool)


```csharp
public Locator GetByPlaceholder(TextMatch placeholder, bool exact = true)
```


Creates a placeholder query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `placeholder` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L467)

<a id="E2E_Locator_GetByRole_"></a>
<a id="E2E_Locator_GetByRole_System_String_E2E_RoleOptions_"></a>

### GetByRole(string, RoleOptions)


```csharp
public Locator GetByRole(string role, RoleOptions options)
```


Creates a role query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [RoleOptions](/api/E2E/RoleOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L443)

<a id="E2E_Locator_GetByRole_System_String_E2E_TextMatch_E2E_RoleOptions_"></a>

### GetByRole(string, TextMatch, RoleOptions)


```csharp
public Locator GetByRole(string role, TextMatch name, RoleOptions options)
```


Creates a role query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [RoleOptions](/api/E2E/RoleOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L446)

<a id="E2E_Locator_GetByRole_System_String_E2E_TextMatch_System_Boolean_"></a>

### GetByRole(string, TextMatch?, bool)


```csharp
public Locator GetByRole(string role, TextMatch? name = null, bool exact = true)
```


Creates a role query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L440)

<a id="E2E_Locator_GetByTestId_"></a>
<a id="E2E_Locator_GetByTestId_E2E_TextMatch_"></a>

### GetByTestId(TextMatch)


```csharp
public Locator GetByTestId(TextMatch testId)
```


Creates a test-id query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testId` | [TextMatch](/api/E2E/TextMatch/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L461)

<a id="E2E_Locator_GetByTestId_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByTestId(TextMatch, TextMatchOptions)


```csharp
public Locator GetByTestId(TextMatch testId, TextMatchOptions options)
```


Creates a test-id query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testId` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L464)

<a id="E2E_Locator_GetByText_"></a>
<a id="E2E_Locator_GetByText_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByText(TextMatch, TextMatchOptions)


```csharp
public Locator GetByText(TextMatch text, TextMatchOptions options)
```


Creates a visible-text query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L452)

<a id="E2E_Locator_GetByText_E2E_TextMatch_System_Boolean_"></a>

### GetByText(TextMatch, bool)


```csharp
public Locator GetByText(TextMatch text, bool exact = true)
```


Creates a visible-text query among this locator's descendants.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L449)

<a id="E2E_Locator_InputValueAsync_"></a>
<a id="E2E_Locator_InputValueAsync_System_Threading_CancellationToken_"></a>

### InputValueAsync(CancellationToken)


```csharp
public Task<string> InputValueAsync(CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads an input value once, the empty string when the node has none. Does not retry for a value and does not verify an earlier `act`. A checkbox or
radio reads its value attribute, `on` when it has none, whatever its checked state. Fails with
`POLICY_DENIED` on a secure field.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L576)

<a id="E2E_Locator_IsCheckedAsync_"></a>
<a id="E2E_Locator_IsCheckedAsync_System_Threading_CancellationToken_"></a>

### IsCheckedAsync(CancellationToken)


```csharp
public Task<bool> IsCheckedAsync(CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads its checked state once.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L616)

<a id="E2E_Locator_IsDisabledAsync_"></a>
<a id="E2E_Locator_IsDisabledAsync_System_Threading_CancellationToken_"></a>

### IsDisabledAsync(CancellationToken)


```csharp
public Task<bool> IsDisabledAsync(CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads its disabled state once, the negation of [IsEnabledAsync(CancellationToken)](#E2E_Locator_IsEnabledAsync_System_Threading_CancellationToken_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L610)

<a id="E2E_Locator_IsEnabledAsync_"></a>
<a id="E2E_Locator_IsEnabledAsync_System_Threading_CancellationToken_"></a>

### IsEnabledAsync(CancellationToken)


```csharp
public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads its enabled state once.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L603)

<a id="E2E_Locator_IsHiddenAsync_"></a>
<a id="E2E_Locator_IsHiddenAsync_System_Threading_CancellationToken_"></a>

### IsHiddenAsync(CancellationToken)


```csharp
public Task<bool> IsHiddenAsync(CancellationToken cancellationToken = default)
```


Reads current hidden or absent state, the negation of [IsVisibleAsync(CancellationToken)](#E2E_Locator_IsVisibleAsync_System_Threading_CancellationToken_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L597)

<a id="E2E_Locator_IsVisibleAsync_"></a>
<a id="E2E_Locator_IsVisibleAsync_System_Threading_CancellationToken_"></a>

### IsVisibleAsync(CancellationToken)


```csharp
public Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default)
```


Reads current visibility: true when exactly one visible node matches. More than one match fails.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;bool&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L591)

<a id="E2E_Locator_Last_"></a>
<a id="E2E_Locator_Last"></a>

### Last()


```csharp
public Locator Last()
```


**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L431)

<a id="E2E_Locator_Nth_"></a>
<a id="E2E_Locator_Nth_System_Int32_"></a>

### Nth(int)


```csharp
public Locator Nth(int index)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [Locator](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L433)

<a id="E2E_Locator_Or_"></a>
<a id="E2E_Locator_Or_E2E_Locator_"></a>

### Or(Locator)


```csharp
public Locator Or(Locator other)
```


Matches either locator, preserving document order without duplicate nodes. `a.Or(b)` stays strict: when both match
different nodes, an action fails with `STRICT_MODE`, so add [First()](#E2E_Locator_First) if either will do. Both locators must come
from the same screen and attempt. Upstream's `locator.or`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `other` | [Locator](./) |  |

**Returns:** [Locator](./)

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `INVALID_LOCATOR`: &lt;code class="paramref"&gt;other&lt;/code&gt; belongs to another screen or attempt. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L416)

<a id="E2E_Locator_PressAsync_"></a>
<a id="E2E_Locator_PressAsync_System_String_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### PressAsync(string, ActionOptions?, CancellationToken)


```csharp
public Task PressAsync(string key, ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L523)

<a id="E2E_Locator_PressSequentiallyAsync_"></a>
<a id="E2E_Locator_PressSequentiallyAsync_System_String_E2E_PressSequentiallyOptions_System_Threading_CancellationToken_"></a>

### PressSequentiallyAsync(string, PressSequentiallyOptions?, CancellationToken)


```csharp
public Task PressSequentiallyAsync(string text, PressSequentiallyOptions? options = null, CancellationToken cancellationToken = default)
```


Focuses the input and types &lt;code class="paramref"&gt;text&lt;/code&gt; one character at a time,
so the app receives key events. [Delay](/api/E2E/PressSequentiallyOptions/#E2E_PressSequentiallyOptions_Delay) waits between characters.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [PressSequentiallyOptions](/api/E2E/PressSequentiallyOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L512)

<a id="E2E_Locator_ScrollIntoViewAsync_"></a>
<a id="E2E_Locator_ScrollIntoViewAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### ScrollIntoViewAsync(ActionOptions?, CancellationToken)


```csharp
public Task ScrollIntoViewAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L556)

<a id="E2E_Locator_ScrollUntilVisibleAsync_"></a>
<a id="E2E_Locator_ScrollUntilVisibleAsync_E2E_Locator_E2E_ScrollUntilVisibleOptions_System_Threading_CancellationToken_"></a>

### ScrollUntilVisibleAsync(Locator, ScrollUntilVisibleOptions?, CancellationToken)


```csharp
public Task ScrollUntilVisibleAsync(Locator target, ScrollUntilVisibleOptions? options = null, CancellationToken cancellationToken = default)
```


Scrolls this locator's node until &lt;code class="paramref"&gt;target&lt;/code&gt; is visible: the node itself swipes, so a scroll container pages
without the pointer having to hover it first. Both locators must come from the same screen. Upstream's `locator.scrollUntilVisible`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [Locator](./) |  |
| `options` | [ScrollUntilVisibleOptions](/api/E2E/ScrollUntilVisibleOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L849)

<a id="E2E_Locator_SelectOptionAsync_"></a>
<a id="E2E_Locator_SelectOptionAsync_System_String_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### SelectOptionAsync(string, ActionOptions?, CancellationToken)


```csharp
public Task SelectOptionAsync(string value, ActionOptions? options = null, CancellationToken cancellationToken = default)
```


Selects the option whose value or label is &lt;code class="paramref"&gt;value&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L545)

<a id="E2E_Locator_TapAsync_"></a>
<a id="E2E_Locator_TapAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### TapAsync(ActionOptions?, CancellationToken)


```csharp
public Task TapAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L478)

<a id="E2E_Locator_TextContentAsync_"></a>
<a id="E2E_Locator_TextContentAsync_System_Threading_CancellationToken_"></a>

### TextContentAsync(CancellationToken)


```csharp
public Task<string?> TextContentAsync(CancellationToken cancellationToken = default)
```


Waits up to the action timeout for one match, then reads whitespace-normalized text once: the node's rendered text (`innerText` on the web), not its label. Null when the node has no text.
Does not retry for a value and does not verify an earlier `act`. Fails with `POLICY_DENIED` on a secure field.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L565)

<a id="E2E_Locator_UncheckAsync_"></a>
<a id="E2E_Locator_UncheckAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### UncheckAsync(ActionOptions?, CancellationToken)


```csharp
public Task UncheckAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L534)

<a id="E2E_Locator_WaitForAsync_"></a>
<a id="E2E_Locator_WaitForAsync_E2E_LocatorWaitForOptions_System_Threading_CancellationToken_"></a>

### WaitForAsync(LocatorWaitForOptions?, CancellationToken)


```csharp
public Task WaitForAsync(LocatorWaitForOptions? options = null, CancellationToken cancellationToken = default)
```


Waits for [State](/api/E2E/LocatorWaitForOptions/#E2E_LocatorWaitForOptions_State), [Visible](/api/E2E/WaitForState/#E2E_WaitForState_Visible) by default:
[Attached](/api/E2E/WaitForState/#E2E_WaitForState_Attached) for one match, visible or not;
[Detached](/api/E2E/WaitForState/#E2E_WaitForState_Detached) for none; [Hidden](/api/E2E/WaitForState/#E2E_WaitForState_Hidden) for none
or a hidden one. The timeout defaults to the action timeout. More than one match fails at once.
Does not verify an earlier `act`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [LocatorWaitForOptions](/api/E2E/LocatorWaitForOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L670)
