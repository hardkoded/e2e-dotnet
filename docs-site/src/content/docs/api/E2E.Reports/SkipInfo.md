---
title: "SkipInfo"
description: "Why a test or an attempt skipped (upstream's SkipInfo)."
editUrl: false
prev: false
next: false
sidebar:
  label: "SkipInfo"
  hidden: true
---

<a id="E2E_Reports_SkipInfo"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record SkipInfo : IEquatable<SkipInfo>
```


Why a test or an attempt skipped (upstream's `SkipInfo`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SkipInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L320)

## Properties

<a id="E2E_Reports_SkipInfo_Cause_"></a>
<a id="E2E_Reports_SkipInfo_Cause"></a>

### Cause


```csharp
public SkipCause Cause { get; init; }
```


**Value:** [SkipCause](/api/E2E.Reports/SkipCause/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L322)

<a id="E2E_Reports_SkipInfo_Filter_"></a>
<a id="E2E_Reports_SkipInfo_Filter"></a>

### Filter


```csharp
public string? Filter { get; init; }
```


The name of the test filter that left the test out; only with cause [Filtered](/api/E2E.Reports/SkipCause/#E2E_Reports_SkipCause_Filtered).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L329)

<a id="E2E_Reports_SkipInfo_Reason_"></a>
<a id="E2E_Reports_SkipInfo_Reason"></a>

### Reason


```csharp
public string Reason { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L324)

<a id="E2E_Reports_SkipInfo_RelatedId_"></a>
<a id="E2E_Reports_SkipInfo_RelatedId"></a>

### RelatedId


```csharp
public string? RelatedId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L326)
