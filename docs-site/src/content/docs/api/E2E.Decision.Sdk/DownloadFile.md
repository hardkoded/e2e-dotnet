---
title: "DownloadFile"
description: "The type of the AI SDK's download (typeof originalDownload), which Create takes. Receives the requested URL and the signal of the enclosing call."
editUrl: false
prev: false
next: false
sidebar:
  label: "DownloadFile"
  hidden: true
---

<a id="E2E_Decision_Sdk_DownloadFile"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public delegate Task<DownloadedFile> DownloadFile(DownloadRequest request, CancellationToken cancellationToken)
```


The type of the AI SDK's `download` (`typeof originalDownload`), which `Create` takes. Receives the requested URL and the signal of the enclosing call.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [DownloadRequest](/api/E2E.Decision.Sdk/DownloadRequest/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;DownloadedFile&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DownloadFunction.cs)
