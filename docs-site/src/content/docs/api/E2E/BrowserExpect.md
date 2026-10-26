---
title: "BrowserExpect"
description: "Polling assertions for the Browser: its URL and its title. Like the locator matchers, a passing one verifies the previous agent.act. A read is bounded by the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserExpect"
  hidden: true
---

<a id="E2E_BrowserExpect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BrowserExpect
```


Polling assertions for the [Browser](/api/E2E/Browser/): its URL and its title. Like the
locator matchers, a passing one verifies the previous `agent.act`. A read is
bounded by the matcher's own deadline, so a page that stops answering ends the
assertion at its timeout.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/BrowserExpect.cs#L17)

## Properties

<a id="E2E_BrowserExpect_Not_"></a>
<a id="E2E_BrowserExpect_Not"></a>

### Not


```csharp
public BrowserExpect Not { get; }
```


Inverts the matcher. A negated matcher passes after its condition has been false for 1000 ms in a row.

**Value:** [BrowserExpect](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/BrowserExpect.cs#L29)

## Methods

<a id="E2E_BrowserExpect_ToHaveTitleAsync_"></a>
<a id="E2E_BrowserExpect_ToHaveTitleAsync_E2E_TextMatch_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveTitleAsync(TextMatch, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveTitleAsync(TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for the title to equal &lt;code class="paramref"&gt;expected&lt;/code&gt; after whitespace normalization, or to match it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/BrowserExpect.cs#L61)

<a id="E2E_BrowserExpect_ToHaveURLAsync_"></a>
<a id="E2E_BrowserExpect_ToHaveURLAsync_System_String_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveURLAsync(string, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveURLAsync(string expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for the URL to equal &lt;code class="paramref"&gt;expected&lt;/code&gt;, resolved against the base URL.
Both sides are compared normalized (default port, trailing slash, dot segments, percent-encoding).
&lt;code class="paramref"&gt;ignoreCase&lt;/code&gt; compares without case.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/BrowserExpect.cs#L36)

<a id="E2E_BrowserExpect_ToHaveURLAsync_System_Text_RegularExpressions_Regex_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveURLAsync(Regex, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveURLAsync(Regex expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


Waits for the URL to match &lt;code class="paramref"&gt;expected&lt;/code&gt;, tested against the complete URL.
&lt;code class="paramref"&gt;ignoreCase&lt;/code&gt; true adds [IgnoreCase](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regexoptions#system-text-regularexpressions-regexoptions-ignorecase) to the pattern, and false removes it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/BrowserExpect.cs#L48)
