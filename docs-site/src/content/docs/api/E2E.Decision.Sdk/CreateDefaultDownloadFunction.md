---
title: "CreateDefaultDownloadFunction"
description: "The AI SDK's createDefaultDownloadFunction (util/download/download-function.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "CreateDefaultDownloadFunction"
  hidden: true
---

<a id="E2E_Decision_Sdk_CreateDefaultDownloadFunction"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class CreateDefaultDownloadFunction
```


The AI SDK's `createDefaultDownloadFunction` (`util/download/download-function.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DownloadFunction.cs)

## Methods

<a id="E2E_Decision_Sdk_CreateDefaultDownloadFunction_Create_"></a>
<a id="E2E_Decision_Sdk_CreateDefaultDownloadFunction_Create_E2E_Decision_Sdk_DownloadFile_System_Nullable_System_Threading_CancellationToken__"></a>

### Create(DownloadFile?, CancellationToken?)


```csharp
public static DownloadFunction Create(DownloadFile? download = null, CancellationToken? cancellationToken = null)
```


Default download function. Downloads the file if it is not supported by the model. &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; (upstream's
`abortSignal`) wins over the token of the call.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `download` | [DownloadFile](/api/E2E.Decision.Sdk/DownloadFile/) | The single-request download. Defaults to the AI SDK's `download`. |
| `cancellationToken` | [CancellationToken?](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) | A token passed to every download instead of the call's. |

**Returns:** [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/)
