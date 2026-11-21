---
title: "Download"
description: "The AI SDK's Download type: the function Create(CreateDownloadOptions) returns. Called with a single URL, it downloads that URL. Called with an array of requ..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Download"
  hidden: true
---

<a id="E2E_Decision_Sdk_Download"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class Download
```


The AI SDK's `Download` type: the function [Create(CreateDownloadOptions)](/api/E2E.Decision.Sdk/CreateDownload/#E2E_Decision_Sdk_CreateDownload_Create_E2E_Decision_Sdk_CreateDownloadOptions_) returns. Called with a single URL, it downloads that
URL. Called with an array of requests, it behaves like the default `experimental_download` of `decide`: URLs the model supports
are passed through (null), all others are downloaded. A [Download](./) converts to a [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/), so it can be
passed as [ExperimentalDownload](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_ExperimentalDownload).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/CreateDownload.cs)

## Methods

<a id="E2E_Decision_Sdk_Download_DownloadAsync_"></a>
<a id="E2E_Decision_Sdk_Download_DownloadAsync_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_DownloadRequest__System_Threading_CancellationToken_"></a>

### `DownloadAsync(IReadOnlyList<DownloadRequest>, CancellationToken)`


```csharp
public Task<IReadOnlyList<DownloadedFile?>> DownloadAsync(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken = default)
```


Downloads every URL the model does not support, and answers null for the others.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `requests` | [IReadOnlyList&lt;DownloadRequest&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;DownloadedFile&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Decision_Sdk_Download_DownloadAsync_System_Uri_System_Threading_CancellationToken_"></a>

### DownloadAsync(Uri, CancellationToken)


```csharp
public Task<DownloadedFile> DownloadAsync(Uri url, CancellationToken cancellationToken = default)
```


Downloads a single URL. Throws [DownloadException](/api/E2E.Decision.Sdk/DownloadException/) when the download fails.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `url` | [Uri](https://learn.microsoft.com/dotnet/api/system.uri) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DownloadedFile&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

## Operators

<a id="E2E_Decision_Sdk_Download_op_Implicit_"></a>
<a id="E2E_Decision_Sdk_Download_op_Implicit_E2E_Decision_Sdk_Download__E2E_Decision_Sdk_DownloadFunction"></a>

### implicit operator DownloadFunction(Download)


```csharp
public static implicit operator DownloadFunction(Download download)
```


Converts to the array form, for [ExperimentalDownload](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_ExperimentalDownload).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `download` | [Download](./) |  |

**Returns:** [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/)
