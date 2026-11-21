---
title: "CreateDownloadOptions"
description: "What Create(CreateDownloadOptions) takes (the options of the AI SDK's createDownload)."
editUrl: false
prev: false
next: false
sidebar:
  label: "CreateDownloadOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_CreateDownloadOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class CreateDownloadOptions
```


What [Create(CreateDownloadOptions)](/api/E2E.Decision.Sdk/CreateDownload/#E2E_Decision_Sdk_CreateDownload_Create_E2E_Decision_Sdk_CreateDownloadOptions_) takes (the options of the AI SDK's `createDownload`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/CreateDownload.cs)

## Properties

<a id="E2E_Decision_Sdk_CreateDownloadOptions_Fetch_"></a>
<a id="E2E_Decision_Sdk_CreateDownloadOptions_Fetch"></a>

### Fetch


```csharp
public FetchFunction? Fetch { get; init; }
```


The transport (upstream's `fetch`), for example one that validates DNS at connection time. Defaults to a shared [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient).

**Value:** [FetchFunction](/api/E2E.Decision.Sdk/FetchFunction/)

<a id="E2E_Decision_Sdk_CreateDownloadOptions_MaxBytes_"></a>
<a id="E2E_Decision_Sdk_CreateDownloadOptions_MaxBytes"></a>

### MaxBytes


```csharp
public long? MaxBytes { get; init; }
```


The most bytes a download may hold. Defaults to 2 GiB.

**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)
