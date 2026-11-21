---
title: "SharedV4FileDataUrl"
description: "File data holding a URL that points to the file."
editUrl: false
prev: false
next: false
sidebar:
  label: "SharedV4FileDataUrl"
  hidden: true
---

<a id="E2E_Decision_Sdk_SharedV4FileDataUrl"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record SharedV4FileDataUrl : SharedV4FileData, IEquatable<SharedV4FileData>, IEquatable<SharedV4FileDataUrl>
```


File data holding a URL that points to the file.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

**Implements:** [IEquatable&lt;SharedV4FileData&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;SharedV4FileDataUrl&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/SharedV4.cs)

## Constructors

<a id="E2E_Decision_Sdk_SharedV4FileDataUrl__ctor_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataUrl__ctor_System_Uri_"></a>

### SharedV4FileDataUrl(Uri)


```csharp
public SharedV4FileDataUrl(Uri Url)
```


File data holding a URL that points to the file.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Url` | [Uri](https://learn.microsoft.com/dotnet/api/system.uri) | The URL. |

## Properties

<a id="E2E_Decision_Sdk_SharedV4FileDataUrl_OriginalUrl_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataUrl_OriginalUrl"></a>

### OriginalUrl


```csharp
public string? OriginalUrl { get; init; }
```


The original URL string before it was parsed into a [Uri](https://learn.microsoft.com/dotnet/api/system.uri), to preserve an opaque non-HTTP URI exactly.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_SharedV4FileDataUrl_Url_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataUrl_Url"></a>

### Url


```csharp
public Uri Url { get; init; }
```


The URL.

**Value:** [Uri](https://learn.microsoft.com/dotnet/api/system.uri)
