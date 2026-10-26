---
title: "ProviderRecordingTarget"
description: "Where the attempt's recording lands and who made it, for CancellationToken)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRecordingTarget"
  hidden: true
---

<a id="E2E_Engine_ProviderRecordingTarget"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ProviderRecordingTarget : IEquatable<ProviderRecordingTarget>
```


Where the attempt's recording lands and who made it, for `CancellationToken)`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ProviderRecordingTarget&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L57)

## Properties

<a id="E2E_Engine_ProviderRecordingTarget_ArtifactsDir_"></a>
<a id="E2E_Engine_ProviderRecordingTarget_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public required string ArtifactsDir { get; init; }
```


Absolute attempt artifact directory; a file recording goes into its `video` directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L60)

<a id="E2E_Engine_ProviderRecordingTarget_LeaseId_"></a>
<a id="E2E_Engine_ProviderRecordingTarget_LeaseId"></a>

### LeaseId


```csharp
public required string LeaseId { get; init; }
```


The lease the recording covers, named in error messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L66)

<a id="E2E_Engine_ProviderRecordingTarget_Provider_"></a>
<a id="E2E_Engine_ProviderRecordingTarget_Provider"></a>

### Provider


```csharp
public required string Provider { get; init; }
```


The provider as error messages name it: `browser provider "kernel"`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L63)
