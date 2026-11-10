---
title: "Urls"
description: "URL normalization and origin policy helpers."
editUrl: false
prev: false
next: false
sidebar:
  label: "Urls"
  hidden: true
---

<a id="E2E_Internal_Urls"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public static class Urls
```


URL normalization and origin policy helpers.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Internal_Urls_BaseUrlOf_"></a>
<a id="E2E_Internal_Urls_BaseUrlOf_System_String_"></a>

### BaseUrlOf(string)


```csharp
public static string BaseUrlOf(string href)
```


The base URL as a test reads it (upstream's `baseUrlOf`): &lt;code class="paramref"&gt;href&lt;/code&gt; without its trailing slash, so `$"{BaseUrl}/path"` joins with one slash.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `href` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_Urls_IsImplicitTestHost_"></a>
<a id="E2E_Internal_Urls_IsImplicitTestHost_System_String_"></a>

### IsImplicitTestHost(string)


```csharp
public static bool IsImplicitTestHost(string hostname)
```


True when environment may default to `test` (loopback, `.localhost`, and `.test` hosts).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `hostname` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_Urls_IsLoopbackHost_"></a>
<a id="E2E_Internal_Urls_IsLoopbackHost_System_String_"></a>

### IsLoopbackHost(string)


```csharp
public static bool IsLoopbackHost(string hostname)
```


True for loopback hosts where plain HTTP is allowed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `hostname` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_Urls_PrintableUrl_"></a>
<a id="E2E_Internal_Urls_PrintableUrl_System_String_"></a>

### PrintableUrl(string)


```csharp
public static string PrintableUrl(string url)
```


&lt;code class="paramref"&gt;url&lt;/code&gt; without userinfo, query, or fragment, any of which may carry a credential: safe to print (upstream's
`printableUrl`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_Urls_SameSite_"></a>
<a id="E2E_Internal_Urls_SameSite_System_String_System_String_"></a>

### SameSite(string, string)


```csharp
public static bool SameSite(string url, string site)
```


True when a URL's host is on &lt;code class="paramref"&gt;site&lt;/code&gt; as [SiteOf(string)](#E2E_Internal_Urls_SiteOf_System_String_) reads it (upstream's `sameSite`): the one rule for where
an app's secrets, headers, and basic-auth credentials may go. An unparseable URL is off every site.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `site` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_Urls_SiteOf_"></a>
<a id="E2E_Internal_Urls_SiteOf_System_String_"></a>

### SiteOf(string)


```csharp
public static string SiteOf(string hostname)
```


The registrable-domain approximation (upstream's `siteOf`): the last two labels of a host, or the last three when the last
is a two-letter country code and the one before it a short second-level label (`example.co.uk`, `shop.com.au`). An IP
literal or a single-label host such as `localhost` is a site of its own. Where the approximation errs it errs narrow, except
on shared hosting suffixes such as `vercel.app`, which it reads as one site.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `hostname` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_Urls_UrlMatches_"></a>
<a id="E2E_Internal_Urls_UrlMatches_System_String_System_String_System_String_System_Nullable_System_Boolean__"></a>

### UrlMatches(string, string, string, bool?)


```csharp
public static bool UrlMatches(string current, string expected, string baseHref, bool? ignoreCase = null)
```


True when &lt;code class="paramref"&gt;current&lt;/code&gt; matches &lt;code class="paramref"&gt;expected&lt;/code&gt; (upstream's `urlMatches`): a string resolves against
&lt;code class="paramref"&gt;baseHref&lt;/code&gt; and compares exactly after serialization, folding case when &lt;code class="paramref"&gt;ignoreCase&lt;/code&gt; is true.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `current` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `baseHref` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_Urls_UrlMatches_System_String_System_Text_RegularExpressions_Regex_System_Nullable_System_Boolean__"></a>

### UrlMatches(string, Regex, bool?)


```csharp
public static bool UrlMatches(string current, Regex expected, bool? ignoreCase = null)
```


True when &lt;code class="paramref"&gt;current&lt;/code&gt; matches &lt;code class="paramref"&gt;expected&lt;/code&gt;: a regular expression tests the whole serialized URL, with
&lt;code class="paramref"&gt;ignoreCase&lt;/code&gt; as Playwright's (true adds the `i` flag, false removes it).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `current` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
