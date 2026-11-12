---
title: "IArtifactStore"
description: "Where artifacts go (upstream's ArtifactStore, artifacts.store). The default keeps them under the artifacts directory, and the report records their paths. A h..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IArtifactStore"
  hidden: true
---

<a id="E2E_IArtifactStore"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IArtifactStore
```


Where artifacts go (upstream's `ArtifactStore`, `artifacts.store`). The default keeps them under the artifacts directory, and
the report records their paths. A host-supplied store (object storage, an API) receives each artifact an attempt registers (a download, a video, an attached screenshot or log) as it is produced, not after the
run, and returns its own reference, which the report records as the artifact's `ref` beside the local path. The cloud seam for
evidence, the way [IStepCache](/api/E2E/IStepCache/) is for the replay cache. A failed `PutAsync` never fails the run: the record
simply carries no `ref`. Like every live value, a store never crosses a process boundary. Set it with
[ArtifactStore](/api/E2E/E2ESessionOptions/#E2E_E2ESessionOptions_ArtifactStore), or override `E2EFixture.ArtifactStore`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs)

## Methods

<a id="E2E_IArtifactStore_PutAsync_"></a>
<a id="E2E_IArtifactStore_PutAsync_E2E_StoredArtifact_System_Threading_CancellationToken_"></a>

### PutAsync(StoredArtifact, CancellationToken)


```csharp
Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default)
```


Stores the artifact and returns the reference to record (upstream's `put`, which returns `{ ref }`). An empty reference is not recorded.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `artifact` | [StoredArtifact](/api/E2E/StoredArtifact/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_IArtifactStore_PutLinkAsync_"></a>
<a id="E2E_IArtifactStore_PutLinkAsync_E2E_StoredArtifactLink_System_Threading_CancellationToken_"></a>

### PutLinkAsync(StoredArtifactLink, CancellationToken)


```csharp
Task<string?> PutLinkAsync(StoredArtifactLink link, CancellationToken cancellationToken = default)
```


Receives a video a hosted service keeps (a browser or device provider's own recording): a link with no bytes, so `PutAsync`
never sees it. Returns a reference the report records as the artifact's `ref` beside its `url`; the default returns none, so
a link is only recorded (upstream's optional `putLink`). A passed attempt's link under `retain-on-failure` is never handed
over, since the report leaves it out. A failed call never fails the run, like `PutAsync`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `link` | [StoredArtifactLink](/api/E2E/StoredArtifactLink/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
