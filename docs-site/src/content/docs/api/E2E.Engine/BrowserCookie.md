---
title: "BrowserCookie"
description: "A browser cookie. When setting one, give either Url, or Domain with an optional Path, never both."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserCookie"
  hidden: true
---

<a id="E2E_Engine_BrowserCookie"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserCookie : IEquatable<BrowserCookie>
```


A browser cookie. When setting one, give either [Url](#E2E_Engine_BrowserCookie_Url), or
[Domain](#E2E_Engine_BrowserCookie_Domain) with an optional [Path](#E2E_Engine_BrowserCookie_Path), never both.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserCookie&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L109)

## Properties

<a id="E2E_Engine_BrowserCookie_Domain_"></a>
<a id="E2E_Engine_BrowserCookie_Domain"></a>

### Domain


```csharp
public string? Domain { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L117)

<a id="E2E_Engine_BrowserCookie_Expires_"></a>
<a id="E2E_Engine_BrowserCookie_Expires"></a>

### Expires


```csharp
public long? Expires { get; init; }
```


Unix timestamp in whole seconds. Null is a session cookie.

**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L122)

<a id="E2E_Engine_BrowserCookie_HttpOnly_"></a>
<a id="E2E_Engine_BrowserCookie_HttpOnly"></a>

### HttpOnly


```csharp
public bool? HttpOnly { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L124)

<a id="E2E_Engine_BrowserCookie_Name_"></a>
<a id="E2E_Engine_BrowserCookie_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L111)

<a id="E2E_Engine_BrowserCookie_Path_"></a>
<a id="E2E_Engine_BrowserCookie_Path"></a>

### Path


```csharp
public string? Path { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L119)

<a id="E2E_Engine_BrowserCookie_SameSite_"></a>
<a id="E2E_Engine_BrowserCookie_SameSite"></a>

### SameSite


```csharp
public string? SameSite { get; init; }
```


`Strict`, `Lax`, or `None`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L129)

<a id="E2E_Engine_BrowserCookie_Secure_"></a>
<a id="E2E_Engine_BrowserCookie_Secure"></a>

### Secure


```csharp
public bool? Secure { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L126)

<a id="E2E_Engine_BrowserCookie_Url_"></a>
<a id="E2E_Engine_BrowserCookie_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L115)

<a id="E2E_Engine_BrowserCookie_Value_"></a>
<a id="E2E_Engine_BrowserCookie_Value"></a>

### Value


```csharp
public required string Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L113)
