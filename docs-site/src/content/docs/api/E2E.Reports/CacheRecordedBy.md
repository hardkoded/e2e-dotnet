---
title: "CacheRecordedBy"
description: "The model that recorded a cache entry."
editUrl: false
prev: false
next: false
sidebar:
  label: "CacheRecordedBy"
  hidden: true
---

<a id="E2E_Reports_CacheRecordedBy"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record CacheRecordedBy : IEquatable<CacheRecordedBy>
```


The model that recorded a cache entry.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;CacheRecordedBy&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L704)

## Properties

<a id="E2E_Reports_CacheRecordedBy_Model_"></a>
<a id="E2E_Reports_CacheRecordedBy_Model"></a>

### Model


```csharp
public string Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L708)

<a id="E2E_Reports_CacheRecordedBy_Provider_"></a>
<a id="E2E_Reports_CacheRecordedBy_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L706)
