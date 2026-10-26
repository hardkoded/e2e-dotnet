---
title: "WebMedia"
description: "User preferences a page reads through @media rules and matchMedia, as Playwright's emulateMedia sets them. A feature left <a href=\"https://learn.microsoft.co..."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebMedia"
  hidden: true
---

<a id="E2E_Engine_WebMedia"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record WebMedia : IEquatable<WebMedia>
```


User preferences a page reads through `@media` rules and `matchMedia`, as Playwright's
`emulateMedia` sets them. A feature left &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; keeps the browser's own value,
or the one an earlier call set.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WebMedia&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebMedia.cs#L37)

## Properties

<a id="E2E_Engine_WebMedia_ColorScheme_"></a>
<a id="E2E_Engine_WebMedia_ColorScheme"></a>

### ColorScheme


```csharp
public WebColorScheme? ColorScheme { get; init; }
```


`prefers-color-scheme`.

**Value:** `WebColorScheme?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebMedia.cs#L40)

<a id="E2E_Engine_WebMedia_ReducedMotion_"></a>
<a id="E2E_Engine_WebMedia_ReducedMotion"></a>

### ReducedMotion


```csharp
public WebReducedMotion? ReducedMotion { get; init; }
```


`prefers-reduced-motion`.

**Value:** `WebReducedMotion?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebMedia.cs#L43)
