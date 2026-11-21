---
title: "CreateDownload"
description: "The AI SDK's createDownload (util/download/create-download.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "CreateDownload"
  hidden: true
---

<a id="E2E_Decision_Sdk_CreateDownload"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class CreateDownload
```


The AI SDK's `createDownload` (`util/download/create-download.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/CreateDownload.cs)

## Methods

<a id="E2E_Decision_Sdk_CreateDownload_Create_"></a>
<a id="E2E_Decision_Sdk_CreateDownload_Create_E2E_Decision_Sdk_CreateDownloadOptions_"></a>

### Create(CreateDownloadOptions?)


```csharp
public static Download Create(CreateDownloadOptions? options = null)
```


Creates a download function with configurable options. Pass it as [ExperimentalDownload](/api/E2E.Decision.Sdk/DecideOptions/#E2E_Decision_Sdk_DecideOptions_ExperimentalDownload), or call it with a
single URL.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [CreateDownloadOptions](/api/E2E.Decision.Sdk/CreateDownloadOptions/) |  |

**Returns:** [Download](/api/E2E.Decision.Sdk/Download/)
