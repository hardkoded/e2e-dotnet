---
title: "Expect"
description: "Polling assertions for a Locator, expect.soft, and expect.poll, and the value matchers of expect(value) (Expect.That(value)). A passing locator assertion ver..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Expect"
  hidden: true
---

<a id="E2E_Expect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class Expect
```


Polling assertions for a [Locator](/api/E2E/Locator/), `expect.soft`, and
`expect.poll`, and the value matchers of `expect(value)`
(`Expect.That(value)`). A passing locator assertion verifies the previous
`agent.act`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs)

## Properties

<a id="E2E_Expect_Undefined_"></a>
<a id="E2E_Expect_Undefined"></a>

### Undefined


```csharp
public static object Undefined { get; }
```


JavaScript's `undefined`, as a value a test passes (.NET-only: C# has no `undefined`). `null` is only `null`:
`Expect.That(null).ToBeDefined()` passes, `Expect.That(Expect.Undefined).ToBeDefined()` fails, and `ToEqual` ignores a
member that holds it, as upstream ignores an `undefined` property.

**Value:** [object](https://learn.microsoft.com/dotnet/api/system.object)

## Methods

<a id="E2E_Expect_Any_"></a>
<a id="E2E_Expect_Any_System_Type_"></a>

### Any(Type)


```csharp
public static AsymmetricMatcher Any(Type sample)
```


Matches any instance of &lt;code class="paramref"&gt;sample&lt;/code&gt; (any number type for a number type), inside `ToEqual` and the other structural matchers. `typeof(object)` also matches `null`, as upstream's `expect.any(Object)` does.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sample` | [Type](https://learn.microsoft.com/dotnet/api/system.type) |  |

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_Anything_"></a>
<a id="E2E_Expect_Anything"></a>

### Anything()


```csharp
public static AsymmetricMatcher Anything()
```


Matches everything but `null` and [Undefined](#E2E_Expect_Undefined).

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_ArrayContaining_"></a>
<a id="E2E_Expect_ArrayContaining_System_Collections_Generic_IReadOnlyList_System_Object__"></a>

### `ArrayContaining(IReadOnlyList<object?>)`


```csharp
public static AsymmetricMatcher ArrayContaining(IReadOnlyList<object?> sample)
```


Matches an array that holds every element of &lt;code class="paramref"&gt;sample&lt;/code&gt;, in any order.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sample` | [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_ObjectContaining_"></a>
<a id="E2E_Expect_ObjectContaining_System_Object_"></a>

### ObjectContaining(object)


```csharp
public static AsymmetricMatcher ObjectContaining(object sample)
```


Matches an object that has every member of &lt;code class="paramref"&gt;sample&lt;/code&gt;, equal by `ToEqual` rules.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sample` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_Poll_"></a>
<a id="E2E_Expect_Poll__1_System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task___0___E2E_PollOptions_"></a>

### `Poll<T>(Func<CancellationToken, Task<T>>, PollOptions?)`


```csharp
public static PollExpectation<T> Poll<T>(Func<CancellationToken, Task<T>> read, PollOptions? options = null)
```


Re-reads &lt;code class="paramref"&gt;read&lt;/code&gt; until the chosen matcher holds or the timeout passes.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `read` | [Func&lt;CancellationToken, Task&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `options` | [PollOptions](/api/E2E/PollOptions/) |  |

**Returns:** `PollExpectation&lt;T&gt;`

<a id="E2E_Expect_Poll__1_System_Func_System_Threading_Tasks_Task___0___E2E_PollOptions_"></a>

### `Poll<T>(Func<Task<T>>, PollOptions?)`


```csharp
public static PollExpectation<T> Poll<T>(Func<Task<T>> read, PollOptions? options = null)
```


Re-reads &lt;code class="paramref"&gt;read&lt;/code&gt; until the chosen matcher holds or the timeout passes.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `read` | [Func&lt;Task&lt;T&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `options` | [PollOptions](/api/E2E/PollOptions/) |  |

**Returns:** `PollExpectation&lt;T&gt;`

<a id="E2E_Expect_Poll__1_System_Func___0__E2E_PollOptions_"></a>

### `Poll<T>(Func<T>, PollOptions?)`


```csharp
public static PollExpectation<T> Poll<T>(Func<T> read, PollOptions? options = null)
```


Re-reads &lt;code class="paramref"&gt;read&lt;/code&gt; until the chosen matcher holds or the timeout passes.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `read` | [Func&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.func-1) |  |
| `options` | [PollOptions](/api/E2E/PollOptions/) |  |

**Returns:** `PollExpectation&lt;T&gt;`

<a id="E2E_Expect_Soft_"></a>
<a id="E2E_Expect_Soft_E2E_Browser_"></a>

### Soft(Browser)


```csharp
public static SoftBrowserExpect Soft(Browser browser)
```


The browser matchers, but a failure is kept as [Soft(Locator)](#E2E_Expect_Soft_E2E_Locator_) keeps one.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `browser` | [Browser](/api/E2E/Browser/) |  |

**Returns:** [SoftBrowserExpect](/api/E2E/SoftBrowserExpect/)

<a id="E2E_Expect_Soft_E2E_Locator_"></a>

### Soft(Locator)


```csharp
public static SoftLocatorExpect Soft(Locator locator)
```


The locator matchers, but a failure is kept on the session instead of
thrown and the body runs on. The test fails at the end with every kept
failure. `E2ETest` records each one as an NUnit assertion failure.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `locator` | [Locator](/api/E2E/Locator/) |  |

**Returns:** [SoftLocatorExpect](/api/E2E/SoftLocatorExpect/)

<a id="E2E_Expect_Soft_E2E_Screen_"></a>

### Soft(Screen)


```csharp
public static SoftScreenExpect Soft(Screen screen)
```


The screen matcher, but a failure is kept as [Soft(Locator)](#E2E_Expect_Soft_E2E_Locator_) keeps one.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screen` | [Screen](/api/E2E/Screen/) |  |

**Returns:** [SoftScreenExpect](/api/E2E/SoftScreenExpect/)

<a id="E2E_Expect_Soft__1_E2E_Screen___0_System_String_"></a>

### `Soft<T>(Screen, T, string?)`


```csharp
public static SoftValueExpectation<T> Soft<T>(Screen screen, T actual, string? message = null)
```


The value matchers, but a failure is kept on &lt;code class="paramref"&gt;screen&lt;/code&gt;'s session instead of thrown, as [Soft(Locator)](#E2E_Expect_Soft_E2E_Locator_)
keeps one. Upstream's `expect.soft(value)` finds the running attempt on its own; a .NET call has no ambient attempt, so it names the session
through its screen.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screen` | [Screen](/api/E2E/Screen/) |  |
| `actual` | `T` |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `SoftValueExpectation&lt;T&gt;`

<a id="E2E_Expect_StringContaining_"></a>
<a id="E2E_Expect_StringContaining_System_String_"></a>

### StringContaining(string)


```csharp
public static AsymmetricMatcher StringContaining(string sample)
```


Matches a string that contains &lt;code class="paramref"&gt;sample&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sample` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_StringMatching_"></a>
<a id="E2E_Expect_StringMatching_System_String_"></a>

### StringMatching(string)


```csharp
public static AsymmetricMatcher StringMatching(string sample)
```


Matches a string that matches &lt;code class="paramref"&gt;sample&lt;/code&gt;, read as a regular expression.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sample` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_StringMatching_System_Text_RegularExpressions_Regex_"></a>

### StringMatching(Regex)


```csharp
public static AsymmetricMatcher StringMatching(Regex sample)
```


Matches a string that matches &lt;code class="paramref"&gt;sample&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sample` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |

**Returns:** [AsymmetricMatcher](/api/E2E/AsymmetricMatcher/)

<a id="E2E_Expect_That_"></a>
<a id="E2E_Expect_That_E2E_Browser_"></a>

### That(Browser)


```csharp
public static BrowserExpect That(Browser browser)
```


Waits for the browser's URL or title with [BrowserExpect](/api/E2E/BrowserExpect/). A passing matcher verifies the previous act.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `browser` | [Browser](/api/E2E/Browser/) |  |

**Returns:** [BrowserExpect](/api/E2E/BrowserExpect/)

<a id="E2E_Expect_That_E2E_Locator_"></a>

### That(Locator)


```csharp
public static LocatorExpect That(Locator locator)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `locator` | [Locator](/api/E2E/Locator/) |  |

**Returns:** [LocatorExpect](/api/E2E/LocatorExpect/)

<a id="E2E_Expect_That_E2E_Screen_"></a>

### That(Screen)


```csharp
public static ScreenExpect That(Screen screen)
```


Compares the whole screen against a stored screenshot with `ToHaveScreenshotAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screen` | [Screen](/api/E2E/Screen/) |  |

**Returns:** [ScreenExpect](/api/E2E/ScreenExpect/)

<a id="E2E_Expect_That__1___0_System_String_"></a>

### `That<T>(T, string?)`


```csharp
public static ValueExpectation<T> That<T>(T actual, string? message = null)
```


Synchronous matchers over a plain value, such as `Expect.That(links, "the playground lists its pages").ToBeGreaterThanOrEqual(2)`.
&lt;code class="paramref"&gt;message&lt;/code&gt; opens every failure of the check.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `actual` | `T` |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `ValueExpectation&lt;T&gt;`
