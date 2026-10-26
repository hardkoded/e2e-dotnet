---
title: "ReportLimits"
description: "The limits the run ran under (upstream's ReportLimits)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportLimits"
  hidden: true
---

<a id="E2E_Reports_ReportLimits"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportLimits : IEquatable<ReportLimits>
```


The limits the run ran under (upstream's `ReportLimits`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportLimits&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L175)

## Properties

<a id="E2E_Reports_ReportLimits_MaxAgentContextBytes_"></a>
<a id="E2E_Reports_ReportLimits_MaxAgentContextBytes"></a>

### MaxAgentContextBytes


```csharp
public int MaxAgentContextBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L177)

<a id="E2E_Reports_ReportLimits_MaxEventsPerStep_"></a>
<a id="E2E_Reports_ReportLimits_MaxEventsPerStep"></a>

### MaxEventsPerStep


```csharp
public int MaxEventsPerStep { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L183)

<a id="E2E_Reports_ReportLimits_MaxLedgerBytes_"></a>
<a id="E2E_Reports_ReportLimits_MaxLedgerBytes"></a>

### MaxLedgerBytes


```csharp
public int MaxLedgerBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L179)

<a id="E2E_Reports_ReportLimits_MaxModelTokensPerCall_"></a>
<a id="E2E_Reports_ReportLimits_MaxModelTokensPerCall"></a>

### MaxModelTokensPerCall


```csharp
public int MaxModelTokensPerCall { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L185)

<a id="E2E_Reports_ReportLimits_MaxObservationBytes_"></a>
<a id="E2E_Reports_ReportLimits_MaxObservationBytes"></a>

### MaxObservationBytes


```csharp
public int MaxObservationBytes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L181)
