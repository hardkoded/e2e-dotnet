---
title: "SoftBrowserExpect"
description: "The browser matchers of BrowserExpect, but an ASSERTIONFAILED is kept on the session instead of thrown, as SoftLocatorExpect does."
editUrl: false
prev: false
next: false
sidebar:
  label: "SoftBrowserExpect"
  hidden: true
---

<a id="E2E_SoftBrowserExpect"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SoftBrowserExpect
```


The browser matchers of [BrowserExpect](/api/E2E/BrowserExpect/), but an
`ASSERTION_FAILED` is kept on the session instead of thrown, as [SoftLocatorExpect](/api/E2E/SoftLocatorExpect/) does.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L100)

## Properties

<a id="E2E_SoftBrowserExpect_Not_"></a>
<a id="E2E_SoftBrowserExpect_Not"></a>

### Not


```csharp
public SoftBrowserExpect Not { get; }
```


Inverts the matcher, as [Not](/api/E2E/BrowserExpect/#E2E_BrowserExpect_Not). A failure is still kept, not thrown.

**Value:** [SoftBrowserExpect](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L112)

## Methods

<a id="E2E_SoftBrowserExpect_ToHaveClassAsync_"></a>
<a id="E2E_SoftBrowserExpect_ToHaveClassAsync_E2E_Locator_E2E_TextMatch_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveClassAsync(Locator, TextMatch, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveClassAsync(Locator target, TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `target` | [Locator](/api/E2E/Locator/) |  |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L120)

<a id="E2E_SoftBrowserExpect_ToHaveTitleAsync_"></a>
<a id="E2E_SoftBrowserExpect_ToHaveTitleAsync_E2E_TextMatch_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveTitleAsync(TextMatch, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveTitleAsync(TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L118)

<a id="E2E_SoftBrowserExpect_ToHaveURLAsync_"></a>
<a id="E2E_SoftBrowserExpect_ToHaveURLAsync_System_String_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveURLAsync(string, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveURLAsync(string expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L114)

<a id="E2E_SoftBrowserExpect_ToHaveURLAsync_System_Text_RegularExpressions_Regex_System_Nullable_System_Boolean__System_Nullable_System_TimeSpan__System_Threading_CancellationToken_"></a>

### ToHaveURLAsync(Regex, bool?, TimeSpan?, CancellationToken)


```csharp
public Task ToHaveURLAsync(Regex expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `timeout` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Soft.cs#L116)
