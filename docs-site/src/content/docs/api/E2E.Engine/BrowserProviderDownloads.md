---
title: "BrowserProviderDownloads"
description: "How a download reaches the runner from a browser on another machine. The browser saves every file to its own disk, where the runner cannot open it, so the en..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserProviderDownloads"
  hidden: true
---

<a id="E2E_Engine_BrowserProviderDownloads"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserProviderDownloads : IEquatable<BrowserProviderDownloads>
```


How a download reaches the runner from a browser on another machine. The browser saves every file to its own disk,
where the runner cannot open it, so the engine points the browser's downloads at [Dir](#E2E_Engine_BrowserProviderDownloads_Dir) there and reads
each finished file back through [ReadAsync](#E2E_Engine_BrowserProviderDownloads_ReadAsync).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserProviderDownloads&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L120)

## Properties

<a id="E2E_Engine_BrowserProviderDownloads_Dir_"></a>
<a id="E2E_Engine_BrowserProviderDownloads_Dir"></a>

### Dir


```csharp
public required string Dir { get; init; }
```


Absolute directory on the browser's machine the browser saves downloads to; created by the browser if missing.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L123)

<a id="E2E_Engine_BrowserProviderDownloads_ReadAsync_"></a>
<a id="E2E_Engine_BrowserProviderDownloads_ReadAsync"></a>

### ReadAsync


```csharp
public required Func<BrowserLease, string, BrowserDownloadContext, CancellationToken, Task<byte[]>> ReadAsync { get; init; }
```


The bytes of one finished download, `file` an absolute path under [Dir](#E2E_Engine_BrowserProviderDownloads_Dir), read off the leased browser's disk.

**Value:** [Func&lt;BrowserLease, string, BrowserDownloadContext, CancellationToken, Task&lt;byte\[\]&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-5)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L126)
