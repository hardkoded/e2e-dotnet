---
title: "Expect"
description: "Polling assertions for a Locator, expect.soft, and expect.poll. A passing locator assertion verifies the previous agent.act. Plain value matchers are not por..."
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
`expect.poll`. A passing locator assertion verifies the previous
`agent.act`. Plain value matchers are not ported: use NUnit
`Assert.That`, and `Assert.EnterMultipleScope` for soft value checks.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L20)

## Methods

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L61)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L70)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L77)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L54)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L47)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L29)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L22)

<a id="E2E_Expect_That_E2E_Screen_"></a>

### That(Screen)


```csharp
public static ScreenExpect That(Screen screen)
```


Compares the whole screen against a stored screenshot with `String)`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `screen` | [Screen](/api/E2E/Screen/) |  |

**Returns:** [ScreenExpect](/api/E2E/ScreenExpect/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Expect.cs#L36)
