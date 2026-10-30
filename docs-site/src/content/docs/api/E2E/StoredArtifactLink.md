---
title: "StoredArtifactLink"
description: "One provider-hosted recording, handed to CancellationToken) once the attempt stopped it (upstream's StoredArtifactLink)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StoredArtifactLink"
  hidden: true
---

<a id="E2E_StoredArtifactLink"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record StoredArtifactLink : IEquatable<StoredArtifactLink>
```


One provider-hosted recording, handed to `CancellationToken)` once the attempt stopped it (upstream's `StoredArtifactLink`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StoredArtifactLink&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1106)

## Properties

<a id="E2E_StoredArtifactLink_AttemptId_"></a>
<a id="E2E_StoredArtifactLink_AttemptId"></a>

### AttemptId


```csharp
public required string AttemptId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1117)

<a id="E2E_StoredArtifactLink_Kind_"></a>
<a id="E2E_StoredArtifactLink_Kind"></a>

### Kind


```csharp
public ArtifactKind Kind { get; init; }
```


Always [Video](/api/E2E.Reports/ArtifactKind/#E2E_Reports_ArtifactKind_Video): a hosted recording is the only link there is.

**Value:** [ArtifactKind](/api/E2E.Reports/ArtifactKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1106)

<a id="E2E_StoredArtifactLink_MediaType_"></a>
<a id="E2E_StoredArtifactLink_MediaType"></a>

### MediaType


```csharp
public required string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1111)

<a id="E2E_StoredArtifactLink_Redaction_"></a>
<a id="E2E_StoredArtifactLink_Redaction"></a>

### Redaction


```csharp
public ArtifactRedaction Redaction { get; init; }
```


A recording masks nothing, so a link is always [Incomplete](/api/E2E.Reports/ArtifactRedaction/#E2E_Reports_ArtifactRedaction_Incomplete).

**Value:** [ArtifactRedaction](/api/E2E.Reports/ArtifactRedaction/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1106)

<a id="E2E_StoredArtifactLink_RunId_"></a>
<a id="E2E_StoredArtifactLink_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1113)

<a id="E2E_StoredArtifactLink_StartedAt_"></a>
<a id="E2E_StoredArtifactLink_StartedAt"></a>

### StartedAt


```csharp
public required DateTimeOffset StartedAt { get; init; }
```


When the recording started.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1120)

<a id="E2E_StoredArtifactLink_StepId_"></a>
<a id="E2E_StoredArtifactLink_StepId"></a>

### StepId


```csharp
public string? StepId { get; init; }
```


The step that was running when the recording was registered, if any.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1123)

<a id="E2E_StoredArtifactLink_TestId_"></a>
<a id="E2E_StoredArtifactLink_TestId"></a>

### TestId


```csharp
public required string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1115)

<a id="E2E_StoredArtifactLink_Url_"></a>
<a id="E2E_StoredArtifactLink_Url"></a>

### Url


```csharp
public required string Url { get; init; }
```


The `http(s)` URL the service serves the recording from.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L1109)
