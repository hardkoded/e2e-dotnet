---
title: "DownloadException"
description: "The AI SDK's DownloadError (@ai-sdk/provider-utils): a file the SDK could not download."
editUrl: false
prev: false
next: false
sidebar:
  label: "DownloadException"
  hidden: true
---

<a id="E2E_Decision_Sdk_DownloadException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DownloadException : Exception, ISerializable
```


The AI SDK's `DownloadError` (`@ai-sdk/provider-utils`): a file the SDK could not download.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_DownloadException__ctor_"></a>
<a id="E2E_Decision_Sdk_DownloadException__ctor_System_String_System_Nullable_System_Int32__System_String_System_String_System_Exception_"></a>

### DownloadException(string, int?, string?, string?, Exception?)


```csharp
public DownloadException(string url, int? statusCode = null, string? statusText = null, string? message = null, Exception? innerException = null)
```


Creates the error. Without &lt;code class="paramref"&gt;message&lt;/code&gt;, it reads as upstream's: the status, or the inner error.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `statusText` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `innerException` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

## Properties

<a id="E2E_Decision_Sdk_DownloadException_StatusCode_"></a>
<a id="E2E_Decision_Sdk_DownloadException_StatusCode"></a>

### StatusCode


```csharp
public int? StatusCode { get; }
```


The HTTP status of the answer; null when none came.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_DownloadException_StatusText_"></a>
<a id="E2E_Decision_Sdk_DownloadException_StatusText"></a>

### StatusText


```csharp
public string? StatusText { get; }
```


The reason phrase of the answer.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DownloadException_Url_"></a>
<a id="E2E_Decision_Sdk_DownloadException_Url"></a>

### Url


```csharp
public string Url { get; }
```


The URL that failed.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
