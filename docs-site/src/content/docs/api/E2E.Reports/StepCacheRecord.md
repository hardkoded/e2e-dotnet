---
title: "StepCacheRecord"
description: "How an agent step used the cache (upstream's StepCacheRecord)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepCacheRecord"
  hidden: true
---

<a id="E2E_Reports_StepCacheRecord"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepCacheRecord : IEquatable<StepCacheRecord>
```


How an agent step used the cache (upstream's `StepCacheRecord`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepCacheRecord&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs)

## Properties

<a id="E2E_Reports_StepCacheRecord_Derived_"></a>
<a id="E2E_Reports_StepCacheRecord_Derived"></a>

### Derived


```csharp
public CacheDerived? Derived { get; init; }
```


**Value:** `CacheDerived?`

<a id="E2E_Reports_StepCacheRecord_Detail_"></a>
<a id="E2E_Reports_StepCacheRecord_Detail"></a>

### Detail


```csharp
public string? Detail { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepCacheRecord_Entry_"></a>
<a id="E2E_Reports_StepCacheRecord_Entry"></a>

### Entry


```csharp
public string? Entry { get; init; }
```


The digest of the entry: 64 hexadecimal characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_StepCacheRecord_Mode_"></a>
<a id="E2E_Reports_StepCacheRecord_Mode"></a>

### Mode


```csharp
public CacheMode Mode { get; init; }
```


**Value:** [CacheMode](/api/E2E.Reports/CacheMode/)

<a id="E2E_Reports_StepCacheRecord_NotRecorded_"></a>
<a id="E2E_Reports_StepCacheRecord_NotRecorded"></a>

### NotRecorded


```csharp
public CacheNotRecorded? NotRecorded { get; init; }
```


**Value:** `CacheNotRecorded?`

<a id="E2E_Reports_StepCacheRecord_Reason_"></a>
<a id="E2E_Reports_StepCacheRecord_Reason"></a>

### Reason


```csharp
public CacheMissReason? Reason { get; init; }
```


**Value:** `CacheMissReason?`

<a id="E2E_Reports_StepCacheRecord_RecordedBy_"></a>
<a id="E2E_Reports_StepCacheRecord_RecordedBy"></a>

### RecordedBy


```csharp
public CacheRecordedBy? RecordedBy { get; init; }
```


**Value:** [CacheRecordedBy](/api/E2E.Reports/CacheRecordedBy/)

<a id="E2E_Reports_StepCacheRecord_ReplayedActions_"></a>
<a id="E2E_Reports_StepCacheRecord_ReplayedActions"></a>

### ReplayedActions


```csharp
public int ReplayedActions { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_StepCacheRecord_TotalActions_"></a>
<a id="E2E_Reports_StepCacheRecord_TotalActions"></a>

### TotalActions


```csharp
public int TotalActions { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_StepCacheRecord_Write_"></a>
<a id="E2E_Reports_StepCacheRecord_Write"></a>

### Write


```csharp
public CacheWrite? Write { get; init; }
```


**Value:** `CacheWrite?`
