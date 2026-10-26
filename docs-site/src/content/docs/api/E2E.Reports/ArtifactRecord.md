---
title: "ArtifactRecord"
description: "A file or link an attempt kept (upstream's ArtifactRecord, run/records.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ArtifactRecord"
  hidden: true
---

<a id="E2E_Reports_ArtifactRecord"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ArtifactRecord : IEquatable<ArtifactRecord>
```


A file or link an attempt kept (upstream's `ArtifactRecord`, `run/records.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ArtifactRecord&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L451)

## Properties

<a id="E2E_Reports_ArtifactRecord_Id_"></a>
<a id="E2E_Reports_ArtifactRecord_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


Names the artifact among the attempt's.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L454)

<a id="E2E_Reports_ArtifactRecord_Kind_"></a>
<a id="E2E_Reports_ArtifactRecord_Kind"></a>

### Kind


```csharp
public ArtifactKind Kind { get; init; }
```


**Value:** [ArtifactKind](/api/E2E.Reports/ArtifactKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L456)

<a id="E2E_Reports_ArtifactRecord_MediaType_"></a>
<a id="E2E_Reports_ArtifactRecord_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L458)

<a id="E2E_Reports_ArtifactRecord_Path_"></a>
<a id="E2E_Reports_ArtifactRecord_Path"></a>

### Path


```csharp
public string? Path { get; init; }
```


Relative to the results directory; null for a link a hosted service keeps.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L461)

<a id="E2E_Reports_ArtifactRecord_Producer_"></a>
<a id="E2E_Reports_ArtifactRecord_Producer"></a>

### Producer


```csharp
public ArtifactProducer Producer { get; init; }
```


**Value:** [ArtifactProducer](/api/E2E.Reports/ArtifactProducer/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L478)

<a id="E2E_Reports_ArtifactRecord_Redaction_"></a>
<a id="E2E_Reports_ArtifactRecord_Redaction"></a>

### Redaction


```csharp
public ArtifactRedaction Redaction { get; init; }
```


**Value:** [ArtifactRedaction](/api/E2E.Reports/ArtifactRedaction/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L476)

<a id="E2E_Reports_ArtifactRecord_Ref_"></a>
<a id="E2E_Reports_ArtifactRecord_Ref"></a>

### Ref


```csharp
public string? Ref { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L471)

<a id="E2E_Reports_ArtifactRecord_Sha256_"></a>
<a id="E2E_Reports_ArtifactRecord_Sha256"></a>

### Sha256


```csharp
public string? Sha256 { get; init; }
```


The digest of the file: 64 hexadecimal characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L469)

<a id="E2E_Reports_ArtifactRecord_Size_"></a>
<a id="E2E_Reports_ArtifactRecord_Size"></a>

### Size


```csharp
public long? Size { get; init; }
```


**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L466)

<a id="E2E_Reports_ArtifactRecord_StartedAt_"></a>
<a id="E2E_Reports_ArtifactRecord_StartedAt"></a>

### StartedAt


```csharp
public DateTimeOffset? StartedAt { get; init; }
```


When a recording began.

**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L474)

<a id="E2E_Reports_ArtifactRecord_Url_"></a>
<a id="E2E_Reports_ArtifactRecord_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Where a hosted service keeps it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L464)
