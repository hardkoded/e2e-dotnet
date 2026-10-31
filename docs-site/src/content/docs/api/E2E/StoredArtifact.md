---
title: "StoredArtifact"
description: "One artifact handed to CancellationToken) (upstream's StoredArtifact): the file's bytes, its digest, and the identity the report files it under. Path is the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "StoredArtifact"
  hidden: true
---

<a id="E2E_StoredArtifact"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record StoredArtifact : IEquatable<StoredArtifact>
```


One artifact handed to `CancellationToken)` (upstream's `StoredArtifact`): the file's bytes, its digest, and the
identity the report files it under. [Path](#E2E_StoredArtifact_Path) is the report-relative path the record carries, stable across runs of the same
test, so a host may use it as its own key.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StoredArtifact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1070)

## Properties

<a id="E2E_StoredArtifact_AttemptId_"></a>
<a id="E2E_StoredArtifact_AttemptId"></a>

### AttemptId


```csharp
public required string AttemptId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1097)

<a id="E2E_StoredArtifact_Bytes_"></a>
<a id="E2E_StoredArtifact_Bytes"></a>

### Bytes


```csharp
public required ReadOnlyMemory<byte> Bytes { get; init; }
```


**Value:** [ReadOnlyMemory&lt;byte&gt;](https://learn.microsoft.com/dotnet/api/system.readonlymemory-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1077)

<a id="E2E_StoredArtifact_Kind_"></a>
<a id="E2E_StoredArtifact_Kind"></a>

### Kind


```csharp
public required ArtifactKind Kind { get; init; }
```


[Screenshot](/api/E2E.Reports/ArtifactKind/#E2E_Reports_ArtifactKind_Screenshot), [Video](/api/E2E.Reports/ArtifactKind/#E2E_Reports_ArtifactKind_Video), [Download](/api/E2E.Reports/ArtifactKind/#E2E_Reports_ArtifactKind_Download), or [Log](/api/E2E.Reports/ArtifactKind/#E2E_Reports_ArtifactKind_Log).

**Value:** [ArtifactKind](/api/E2E.Reports/ArtifactKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1073)

<a id="E2E_StoredArtifact_MediaType_"></a>
<a id="E2E_StoredArtifact_MediaType"></a>

### MediaType


```csharp
public required string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1075)

<a id="E2E_StoredArtifact_Path_"></a>
<a id="E2E_StoredArtifact_Path"></a>

### Path


```csharp
public required string Path { get; init; }
```


Report-relative path with `/` separators.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1084)

<a id="E2E_StoredArtifact_Redaction_"></a>
<a id="E2E_StoredArtifact_Redaction"></a>

### Redaction


```csharp
public required ArtifactRedaction Redaction { get; init; }
```


How much of the file the runner masked, as the report records it. A [Download](/api/E2E.Reports/ArtifactKind/#E2E_Reports_ArtifactKind_Download) is
[Incomplete](/api/E2E.Reports/ArtifactRedaction/#E2E_Reports_ArtifactRedaction_Incomplete) unless the runner ran it as text through the session's secret values; a store that exports
only what the runner vouches for reads this rather than the kind.

**Value:** [ArtifactRedaction](/api/E2E.Reports/ArtifactRedaction/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1091)

<a id="E2E_StoredArtifact_RunId_"></a>
<a id="E2E_StoredArtifact_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1093)

<a id="E2E_StoredArtifact_Sha256_"></a>
<a id="E2E_StoredArtifact_Sha256"></a>

### Sha256


```csharp
public required string Sha256 { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1081)

<a id="E2E_StoredArtifact_Size_"></a>
<a id="E2E_StoredArtifact_Size"></a>

### Size


```csharp
public required long Size { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1079)

<a id="E2E_StoredArtifact_StartedAt_"></a>
<a id="E2E_StoredArtifact_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset? StartedAt { get; init; }
```


When a time-based artifact began recording (a video segment), so a host can align step timestamps with it without reading the report.

**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1103)

<a id="E2E_StoredArtifact_StepId_"></a>
<a id="E2E_StoredArtifact_StepId"></a>

### StepId


```csharp
public string? StepId { get; init; }
```


The step that produced it, when one was running.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1100)

<a id="E2E_StoredArtifact_TestId_"></a>
<a id="E2E_StoredArtifact_TestId"></a>

### TestId


```csharp
public required string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1095)
