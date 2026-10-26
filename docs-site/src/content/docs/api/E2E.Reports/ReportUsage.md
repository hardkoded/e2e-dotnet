---
title: "ReportUsage"
description: "What the run used, against ReportLimits (upstream's ReportUsage)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportUsage"
  hidden: true
---

<a id="E2E_Reports_ReportUsage"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportUsage : IEquatable<ReportUsage>
```


What the run used, against [ReportLimits](/api/E2E.Reports/ReportLimits/) (upstream's `ReportUsage`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportUsage&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L189)

## Properties

<a id="E2E_Reports_ReportUsage_ArtifactBytes_"></a>
<a id="E2E_Reports_ReportUsage_ArtifactBytes"></a>

### ArtifactBytes


```csharp
public long ArtifactBytes { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L199)

<a id="E2E_Reports_ReportUsage_DiscoveredResults_"></a>
<a id="E2E_Reports_ReportUsage_DiscoveredResults"></a>

### DiscoveredResults


```csharp
public int DiscoveredResults { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L191)

<a id="E2E_Reports_ReportUsage_Downloads_"></a>
<a id="E2E_Reports_ReportUsage_Downloads"></a>

### Downloads


```csharp
public int Downloads { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L201)

<a id="E2E_Reports_ReportUsage_EstimatedCostUsd_"></a>
<a id="E2E_Reports_ReportUsage_EstimatedCostUsd"></a>

### EstimatedCostUsd


```csharp
public double? EstimatedCostUsd { get; init; }
```


**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L214)

<a id="E2E_Reports_ReportUsage_Events_"></a>
<a id="E2E_Reports_ReportUsage_Events"></a>

### Events


```csharp
public long Events { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L203)

<a id="E2E_Reports_ReportUsage_MaxActionStepsInStep_"></a>
<a id="E2E_Reports_ReportUsage_MaxActionStepsInStep"></a>

### MaxActionStepsInStep


```csharp
public int MaxActionStepsInStep { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L212)

<a id="E2E_Reports_ReportUsage_MaxAgentContextBytes_"></a>
<a id="E2E_Reports_ReportUsage_MaxAgentContextBytes"></a>

### MaxAgentContextBytes


```csharp
public int MaxAgentContextBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L193)

<a id="E2E_Reports_ReportUsage_MaxLedgerBytes_"></a>
<a id="E2E_Reports_ReportUsage_MaxLedgerBytes"></a>

### MaxLedgerBytes


```csharp
public int MaxLedgerBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L195)

<a id="E2E_Reports_ReportUsage_MaxModelCallsInStep_"></a>
<a id="E2E_Reports_ReportUsage_MaxModelCallsInStep"></a>

### MaxModelCallsInStep


```csharp
public int MaxModelCallsInStep { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L210)

<a id="E2E_Reports_ReportUsage_MaxObservationBytes_"></a>
<a id="E2E_Reports_ReportUsage_MaxObservationBytes"></a>

### MaxObservationBytes


```csharp
public int MaxObservationBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L197)

<a id="E2E_Reports_ReportUsage_ModelCachedTokens_"></a>
<a id="E2E_Reports_ReportUsage_ModelCachedTokens"></a>

### ModelCachedTokens


```csharp
public long? ModelCachedTokens { get; init; }
```


Input tokens served from provider prompt caches; null until a step reports the split.

**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L208)

<a id="E2E_Reports_ReportUsage_ModelTokens_"></a>
<a id="E2E_Reports_ReportUsage_ModelTokens"></a>

### ModelTokens


```csharp
public long ModelTokens { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L205)
