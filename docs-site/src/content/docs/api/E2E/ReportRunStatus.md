---
title: "ReportRunStatus"
description: "How a run ended (upstream's BuildReportOptions['status'] | 'blocked')."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportRunStatus"
  hidden: true
---

<a id="E2E_ReportRunStatus"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ReportRunStatus
```


How a run ended (upstream's `BuildReportOptions['status'] | 'blocked'`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/ReportRecords.cs#L63)

## Fields

<a id="E2E_ReportRunStatus_Blocked"></a>

### Blocked


```csharp
Blocked = 4
```


Not passed, and every failing result carries a blockable code: nothing contradicts that the run could not reach a product verdict.

**Value:** [ReportRunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/ReportRecords.cs#L71)

<a id="E2E_ReportRunStatus_Error"></a>

### Error


```csharp
Error = 2
```


**Value:** [ReportRunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/ReportRecords.cs#L67)

<a id="E2E_ReportRunStatus_Failed"></a>

### Failed


```csharp
Failed = 1
```


**Value:** [ReportRunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/ReportRecords.cs#L66)

<a id="E2E_ReportRunStatus_Interrupted"></a>

### Interrupted


```csharp
Interrupted = 3
```


**Value:** [ReportRunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/ReportRecords.cs#L68)

<a id="E2E_ReportRunStatus_Passed"></a>

### Passed


```csharp
Passed = 0
```


**Value:** [ReportRunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/ReportRecords.cs#L65)
