---
title: "DownloadFunction"
description: "The AI SDK's DownloadFunction (util/download/download-function.ts), experimental. Called with the requested URLs, it answers one entry for each, in the same..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DownloadFunction"
  hidden: true
---

<a id="E2E_Decision_Sdk_DownloadFunction"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public delegate Task<IReadOnlyList<DownloadedFile?>> DownloadFunction(IReadOnlyList<DownloadRequest> requests, CancellationToken cancellationToken)
```


The AI SDK's `DownloadFunction` (`util/download/download-function.ts`), experimental. Called with the requested URLs, it answers
one entry for each, in the same order: the downloaded file, or null when the URL goes to the model as is. It throws
[DownloadException](/api/E2E.Decision.Sdk/DownloadException/) when a download fails.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `requests` | [IReadOnlyList&lt;DownloadRequest&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;DownloadedFile&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DownloadFunction.cs)
