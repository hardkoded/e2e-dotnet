---
title: "CookieFilter"
description: "Cookie filters are combined; a string matches the field exactly, a regular expression matches its text."
editUrl: false
prev: false
next: false
sidebar:
  label: "CookieFilter"
  hidden: true
---

<a id="E2E_Engine_CookieFilter"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record CookieFilter : IEquatable<CookieFilter>
```


Cookie filters are combined; a string matches the field exactly, a regular expression matches its text.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;CookieFilter&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L153)

## Properties

<a id="E2E_Engine_CookieFilter_Domain_"></a>
<a id="E2E_Engine_CookieFilter_Domain"></a>

### Domain


```csharp
public TextMatch? Domain { get; init; }
```


**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L157)

<a id="E2E_Engine_CookieFilter_Name_"></a>
<a id="E2E_Engine_CookieFilter_Name"></a>

### Name


```csharp
public TextMatch? Name { get; init; }
```


**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L155)

<a id="E2E_Engine_CookieFilter_Path_"></a>
<a id="E2E_Engine_CookieFilter_Path"></a>

### Path


```csharp
public TextMatch? Path { get; init; }
```


**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L159)
