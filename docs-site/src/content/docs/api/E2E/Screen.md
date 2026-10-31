---
title: "Screen"
description: "Semantic queries against the current screen. An action or a single-node assertion fails when the query matches more than one node. Narrow it with Filter(Text..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Screen"
  hidden: true
---

<a id="E2E_Screen"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class Screen
```


Semantic queries against the current screen. An action or a single-node
assertion fails when the query matches more than one node. Narrow it with
[Filter(TextMatch)](/api/E2E/Locator/#E2E_Locator_Filter_E2E_TextMatch_), [First()](/api/E2E/Locator/#E2E_Locator_First), [Last()](/api/E2E/Locator/#E2E_Locator_Last), or [Nth(int)](/api/E2E/Locator/#E2E_Locator_Nth_System_Int32_).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Derived:** [FrameScreen](/api/E2E/FrameScreen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L16)

## Methods

<a id="E2E_Screen_DebugAsync_"></a>
<a id="E2E_Screen_DebugAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### DebugAsync(ActionOptions?, CancellationToken)


```csharp
public Task DebugAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


Prints `CancellationToken)` to [Out](https://learn.microsoft.com/dotnet/api/system.console.out), which NUnit and xUnit v2 capture into the test's output.
xUnit v3 shows it only when the test assembly has `[assembly: CaptureConsole]`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L197)

<a id="E2E_Screen_GetByDisplayValue_"></a>
<a id="E2E_Screen_GetByDisplayValue_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByDisplayValue(TextMatch, TextMatchOptions)


```csharp
public Locator GetByDisplayValue(TextMatch value, TextMatchOptions options)
```


Creates a lazy displayed-value query: the current value of an input, text area, or select. A checkbox or radio never shows its value, so it never matches.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L184)

<a id="E2E_Screen_GetByDisplayValue_E2E_TextMatch_System_Boolean_"></a>

### GetByDisplayValue(TextMatch, bool)


```csharp
public Locator GetByDisplayValue(TextMatch value, bool exact = true)
```


Creates a lazy displayed-value query: the current value of an input, text area, or select. A checkbox or radio never shows its value, so it never matches.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L181)

<a id="E2E_Screen_GetByLabel_"></a>
<a id="E2E_Screen_GetByLabel_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByLabel(TextMatch, TextMatchOptions)


```csharp
public Locator GetByLabel(TextMatch label, TextMatchOptions options)
```


Creates a lazy accessible-label query: any node whose accessible name matches.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L162)

<a id="E2E_Screen_GetByLabel_E2E_TextMatch_System_Boolean_"></a>

### GetByLabel(TextMatch, bool)


```csharp
public Locator GetByLabel(TextMatch label, bool exact = true)
```


Creates a lazy accessible-label query: any node whose accessible name matches.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L159)

<a id="E2E_Screen_GetByPlaceholder_"></a>
<a id="E2E_Screen_GetByPlaceholder_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByPlaceholder(TextMatch, TextMatchOptions)


```csharp
public Locator GetByPlaceholder(TextMatch placeholder, TextMatchOptions options)
```


Creates a lazy placeholder query.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `placeholder` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L178)

<a id="E2E_Screen_GetByPlaceholder_E2E_TextMatch_System_Boolean_"></a>

### GetByPlaceholder(TextMatch, bool)


```csharp
public Locator GetByPlaceholder(TextMatch placeholder, bool exact = true)
```


Creates a lazy placeholder query.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `placeholder` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L175)

<a id="E2E_Screen_GetByRole_"></a>
<a id="E2E_Screen_GetByRole_System_String_E2E_RoleOptions_"></a>

### GetByRole(string, RoleOptions)


```csharp
public Locator GetByRole(string role, RoleOptions options)
```


Creates a lazy role query narrowed by name, states, or heading level.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `options` | [RoleOptions](/api/E2E/RoleOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L127)

<a id="E2E_Screen_GetByRole_System_String_E2E_TextMatch_E2E_RoleOptions_"></a>

### GetByRole(string, TextMatch, RoleOptions)


```csharp
public Locator GetByRole(string role, TextMatch name, RoleOptions options)
```


Creates a lazy role query narrowed to an accessible name and the given options.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [RoleOptions](/api/E2E/RoleOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L134)

<a id="E2E_Screen_GetByRole_System_String_E2E_TextMatch_System_Boolean_"></a>

### GetByRole(string, TextMatch?, bool)


```csharp
public Locator GetByRole(string role, TextMatch? name = null, bool exact = true)
```


Creates a lazy role query. `img` is read as `image`. A role query never matches a hidden node.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L121)

<a id="E2E_Screen_GetByTestId_"></a>
<a id="E2E_Screen_GetByTestId_E2E_TextMatch_"></a>

### GetByTestId(TextMatch)


```csharp
public Locator GetByTestId(TextMatch testId)
```


Creates a lazy test-id query. A string matches the whole id, case-sensitive.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testId` | [TextMatch](/api/E2E/TextMatch/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L165)

<a id="E2E_Screen_GetByTestId_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByTestId(TextMatch, TextMatchOptions)


```csharp
public Locator GetByTestId(TextMatch testId, TextMatchOptions options)
```


Creates a lazy test-id query. A string matches the whole id, case-sensitive; [Exact](/api/E2E/TextMatchOptions/#E2E_TextMatchOptions_Exact) is ignored.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testId` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L168)

<a id="E2E_Screen_GetByText_"></a>
<a id="E2E_Screen_GetByText_E2E_TextMatch_E2E_TextMatchOptions_"></a>

### GetByText(TextMatch, TextMatchOptions)


```csharp
public Locator GetByText(TextMatch text, TextMatchOptions options)
```


Creates a lazy visible-text query. It answers with the innermost match, such as a
`<strong>` inside a paragraph. Text inside a control's label answers with the control.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [TextMatch](/api/E2E/TextMatch/) |  |
| `options` | [TextMatchOptions](/api/E2E/TextMatchOptions/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L156)

<a id="E2E_Screen_GetByText_E2E_TextMatch_System_Boolean_"></a>

### GetByText(TextMatch, bool)


```csharp
public Locator GetByText(TextMatch text, bool exact = true)
```


Creates a lazy visible-text query. It answers with the innermost match, such as a
`<strong>` inside a paragraph. Text inside a control's label answers with the control.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [Locator](/api/E2E/Locator/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L150)

<a id="E2E_Screen_ScrollUntilVisibleAsync_"></a>
<a id="E2E_Screen_ScrollUntilVisibleAsync_E2E_Locator_E2E_ScrollUntilVisibleOptions_System_Threading_CancellationToken_"></a>

### ScrollUntilVisibleAsync(Locator, ScrollUntilVisibleOptions?, CancellationToken)


```csharp
public Task ScrollUntilVisibleAsync(Locator target, ScrollUntilVisibleOptions? options = null, CancellationToken cancellationToken = default)
```


Scrolls the viewport until &lt;code class="paramref"&gt;target&lt;/code&gt; is visible: polls the target and swipes between polls, down and slowly
unless [ScrollUntilVisibleOptions](/api/E2E/ScrollUntilVisibleOptions/) say otherwise. Every swipe is bounded by the scroll's own deadline, not the
action timeout. Upstream's `screen.scrollUntilVisible`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [Locator](/api/E2E/Locator/) |  |
| `options` | [ScrollUntilVisibleOptions](/api/E2E/ScrollUntilVisibleOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `LOCATOR_NOT_FOUND` when the target did not become visible in time; `INVALID_LOCATOR` when it belongs to another screen or attempt. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L206)

<a id="E2E_Screen_TreeAsync_"></a>
<a id="E2E_Screen_TreeAsync_E2E_ActionOptions_System_Threading_CancellationToken_"></a>

### TreeAsync(ActionOptions?, CancellationToken)


```csharp
public Task<string> TreeAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
```


Reads the whole screen once, without waiting, as the agent reads it: one node per line, `#id role "name" text="…" [states]`,
children indented, secrets redacted. The text has the format and size limit of the trace's screen at failure. A node the page does
not show is left out. Use it to write a locator. Takes [Timeout](/api/E2E/ActionOptions/#E2E_ActionOptions_Timeout), the configured action timeout by default.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ActionOptions](/api/E2E/ActionOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L191)
