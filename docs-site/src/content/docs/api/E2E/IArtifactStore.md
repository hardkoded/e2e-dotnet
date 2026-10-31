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
evidence, the way [IStepCache](/api/E2E/IStepCache/) is for the replay cache. A failed `CancellationToken)` never fails the run: the record
simply carries no `ref`. Like every live value, a store never crosses a process boundary. Set it with
[ArtifactStore](/api/E2E/E2ESessionOptions/#E2E_E2ESessionOptions_ArtifactStore), or override `E2EFixture.ArtifactStore`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1141)
