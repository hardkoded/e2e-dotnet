---
title: "IBrowserProvider"
description: "A source of hosted browsers: the seam a hosted browser service plugs into (upstream BrowserProvider). In Worker scope AcquireAsync is called once per worker..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IBrowserProvider"
  hidden: true
---

<a id="E2E_Engine_IBrowserProvider"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IBrowserProvider
```


A source of hosted browsers: the seam a hosted browser service plugs into (upstream `BrowserProvider`). In
[Worker](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Worker) scope `AcquireAsync` is called once per worker slot, for every
slot at once, in the runner; a worker whose browser drops calls it again, from its own process, for a replacement.
In [Attempt](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Attempt) scope every attempt start calls it from the worker. `ReleaseAsync`
is called exactly once per lease, by the process that acquired it, also after an acquire of another slot failed; a worker
that dies first never calls it, and `SweepAsync` releases what it left. A provider must not rely on
runner-process state to serve a request from a worker. Nothing here knows any vendor.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs)

## Properties

<a id="E2E_Engine_IBrowserProvider_Downloads_"></a>
<a id="E2E_Engine_IBrowserProvider_Downloads"></a>

### Downloads


```csharp
BrowserProviderDownloads? Downloads { get; }
```


Serves `browser.waitForDownload` for a browser that runs on another machine. Without it a download on such
a browser fails, since the file lands on the browser's disk; a provider whose browsers run on the runner's own
machine leaves it out.

**Value:** [BrowserProviderDownloads](/api/E2E.Engine/BrowserProviderDownloads/)

<a id="E2E_Engine_IBrowserProvider_Name_"></a>
<a id="E2E_Engine_IBrowserProvider_Name"></a>

### Name


```csharp
string Name { get; }
```


Label in progress lines and error messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_IBrowserProvider_Scope_"></a>
<a id="E2E_Engine_IBrowserProvider_Scope"></a>

### Scope


```csharp
BrowserProviderScope Scope { get; }
```


How long a lease lives; [Worker](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Worker) by default.

**Value:** [BrowserProviderScope](/api/E2E.Engine/BrowserProviderScope/)

## Methods

<a id="E2E_Engine_IBrowserProvider_AcquireAsync_"></a>
<a id="E2E_Engine_IBrowserProvider_AcquireAsync_E2E_Engine_BrowserRequest_System_Threading_CancellationToken_"></a>

### AcquireAsync(BrowserRequest, CancellationToken)


```csharp
Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [BrowserRequest](/api/E2E.Engine/BrowserRequest/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;BrowserLease&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserProvider_CdpHeadersAsync_"></a>
<a id="E2E_Engine_IBrowserProvider_CdpHeadersAsync_E2E_Engine_BrowserLease_E2E_Engine_BrowserRequest_System_Threading_CancellationToken_"></a>

### CdpHeadersAsync(BrowserLease, BrowserRequest, CancellationToken)


```csharp
Task<IReadOnlyDictionary<string, string>?> CdpHeadersAsync(BrowserLease lease, BrowserRequest context, CancellationToken cancellationToken)
```


Resolves CDP handshake headers in the worker from its own environment; never serialized with the lease. Null,
the default, sends none.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `lease` | [BrowserLease](/api/E2E.Engine/BrowserLease/) |  |
| `context` | [BrowserRequest](/api/E2E.Engine/BrowserRequest/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyDictionary&lt;string, string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserProvider_RecordAsync_"></a>
<a id="E2E_Engine_IBrowserProvider_RecordAsync_E2E_Engine_BrowserLease_E2E_Engine_ProviderRecordContext_System_Threading_CancellationToken_"></a>

### RecordAsync(BrowserLease, ProviderRecordContext, CancellationToken)


```csharp
Task<ProviderRecording?> RecordAsync(BrowserLease lease, ProviderRecordContext context, CancellationToken cancellationToken)
```


Records the leased browser for an attempt that records video, in place of the engine's screencast of the page: the
service's own recording of the browser, called from the worker when the attempt starts its video. A provider that
cannot record its browsers (a headless session the service cannot replay) returns null, and the engine records the
screencast.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `lease` | [BrowserLease](/api/E2E.Engine/BrowserLease/) |  |
| `context` | [ProviderRecordContext](/api/E2E.Engine/ProviderRecordContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ProviderRecording&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_IBrowserProvider_ReleaseAsync_"></a>
<a id="E2E_Engine_IBrowserProvider_ReleaseAsync_E2E_Engine_BrowserLease_E2E_Engine_BrowserReleaseContext_System_Threading_CancellationToken_"></a>

### ReleaseAsync(BrowserLease, BrowserReleaseContext, CancellationToken)


```csharp
Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `lease` | [BrowserLease](/api/E2E.Engine/BrowserLease/) |  |
| `context` | [BrowserReleaseContext](/api/E2E.Engine/BrowserReleaseContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_IBrowserProvider_SweepAsync_"></a>
<a id="E2E_Engine_IBrowserProvider_SweepAsync_E2E_Engine_BrowserReleaseContext_System_Threading_CancellationToken_"></a>

### SweepAsync(BrowserReleaseContext, CancellationToken)


```csharp
Task<IReadOnlyList<string>> SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken)
```


Releases every lease of the run and target the provider still holds open, and resolves to their ids. Called once
per target from finish, in the runner, after every worker exited and the leases prepare made were released:
whatever it finds is a lease a worker acquired (a per-attempt browser, a replacement) and never gave back,
because the worker died before it could. Find them by what acquire tagged them with at the service: the run id
and the target.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [BrowserReleaseContext](/api/E2E.Engine/BrowserReleaseContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
