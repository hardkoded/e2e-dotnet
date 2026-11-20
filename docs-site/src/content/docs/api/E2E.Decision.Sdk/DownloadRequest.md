---
title: "DownloadRequest"
description: "One URL a DownloadFunction is asked for (an element of the options array of upstream's DownloadFunction)."
editUrl: false
prev: false
next: false
sidebar:
  label: "DownloadRequest"
  hidden: true
---

<a id="E2E_Decision_Sdk_DownloadRequest"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DownloadRequest : IEquatable<DownloadRequest>
```


One URL a [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/) is asked for (an element of the options array of upstream's `DownloadFunction`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DownloadRequest&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DownloadFunction.cs)

## Constructors

<a id="E2E_Decision_Sdk_DownloadRequest__ctor_"></a>
<a id="E2E_Decision_Sdk_DownloadRequest__ctor_System_Uri_System_Boolean_"></a>

### DownloadRequest(Uri, bool)


```csharp
public DownloadRequest(Uri Url, bool IsUrlSupportedByModel)
```


One URL a [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/) is asked for (an element of the options array of upstream's `DownloadFunction`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Url` | [Uri](https://learn.microsoft.com/dotnet/api/system.uri) | The URL of the file. |
| `IsUrlSupportedByModel` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) | Whether the model takes the URL as is. Decision models take no URLs, so `decide` always says false. |

## Properties

<a id="E2E_Decision_Sdk_DownloadRequest_IsUrlSupportedByModel_"></a>
<a id="E2E_Decision_Sdk_DownloadRequest_IsUrlSupportedByModel"></a>

### IsUrlSupportedByModel


```csharp
public bool IsUrlSupportedByModel { get; init; }
```


Whether the model takes the URL as is. Decision models take no URLs, so `decide` always says false.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_DownloadRequest_Url_"></a>
<a id="E2E_Decision_Sdk_DownloadRequest_Url"></a>

### Url


```csharp
public Uri Url { get; init; }
```


The URL of the file.

**Value:** [Uri](https://learn.microsoft.com/dotnet/api/system.uri)
