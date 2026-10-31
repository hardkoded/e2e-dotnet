---
title: "FrameScreen"
description: "The screen scope FrameLocator(string) returns. Every Screen query answers from inside the frame, and the web-only escape hatches follow it in: Locator(string..."
editUrl: false
prev: false
next: false
sidebar:
  label: "FrameScreen"
  hidden: true
---

<a id="E2E_FrameScreen"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class FrameScreen : Screen
```


The screen scope [FrameLocator(string)](/api/E2E/Browser/#E2E_Browser_FrameLocator_System_String_) returns. Every [Screen](/api/E2E/Screen/) query answers from inside the frame, and the
web-only escape hatches follow it in: [Locator(string)](#E2E_FrameScreen_Locator_System_String_) for a CSS or XPath selector on a control with no accessible
name, and [FrameLocator(string)](#E2E_FrameScreen_FrameLocator_System_String_) for a frame nested inside this one. Upstream's `FrameScreen`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Screen](/api/E2E/Screen/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L777)

## Methods

<a id="E2E_FrameScreen_FrameLocator_"></a>
<a id="E2E_FrameScreen_FrameLocator_System_String_"></a>

### FrameLocator(string)


```csharp
public FrameScreen FrameLocator(string selector)
```


Creates a screen query scope inside an iframe nested in this frame.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `selector` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [FrameScreen](./)

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `INVALID_LOCATOR`: &lt;code class="paramref"&gt;selector&lt;/code&gt; is empty. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L794)

<a id="E2E_FrameScreen_Locator_"></a>
<a id="E2E_FrameScreen_Locator_System_String_"></a>

### Locator(string)


```csharp
public Locator Locator(string selector)
```


Creates a web-only CSS or XPath locator inside this frame.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `selector` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Locator](/api/E2E/Locator/)

**Exceptions**

| Type | Condition |
| --- | --- |
| [TestException](/api/E2E/TestException/) | `INVALID_LOCATOR`: &lt;code class="paramref"&gt;selector&lt;/code&gt; is empty. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs#L789)
