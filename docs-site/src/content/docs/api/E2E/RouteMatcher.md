---
title: "RouteMatcher"
description: "A URL pattern narrowed to one HTTP method, for RouteAsync, UnrouteAsync and WaitForResponseAsync. Set Method and exactly one of Url and UrlRegex; a missing o..."
editUrl: false
prev: false
next: false
sidebar:
  label: "RouteMatcher"
  hidden: true
---

<a id="E2E_RouteMatcher"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record RouteMatcher : IEquatable<RouteMatcher>
```


A URL pattern narrowed to one HTTP method, for `RouteAsync`,
`UnrouteAsync` and
`WaitForResponseAsync`. Set [Method](#E2E_RouteMatcher_Method) and exactly one of
[Url](#E2E_RouteMatcher_Url) and [UrlRegex](#E2E_RouteMatcher_UrlRegex); a missing or invalid part fails with `INVALID_ARGUMENT`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RouteMatcher&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Browser.cs)

## Properties

<a id="E2E_RouteMatcher_Method_"></a>
<a id="E2E_RouteMatcher_Method"></a>

### Method


```csharp
public string? Method { get; init; }
```


The HTTP method to match, case-insensitive: `"POST"` and `"post"` are the same.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RouteMatcher_Url_"></a>
<a id="E2E_RouteMatcher_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


The URL pattern, a string using the same glob grammar as a plain route pattern. [UrlRegex](#E2E_RouteMatcher_UrlRegex) is the regex form.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RouteMatcher_UrlRegex_"></a>
<a id="E2E_RouteMatcher_UrlRegex"></a>

### UrlRegex


```csharp
public Regex? UrlRegex { get; init; }
```


The URL pattern as a [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex). `RouteAsync` and `WaitForResponseAsync` match requests against it; `UnrouteAsync` compares it structurally.

**Value:** [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex)
