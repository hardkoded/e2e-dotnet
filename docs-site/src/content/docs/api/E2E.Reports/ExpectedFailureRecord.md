---
title: "ExpectedFailureRecord"
description: "Why an attempt or a test was expected to fail (upstream's inline expectedFailure object of ReportAttempt and ReportResult, report/build.ts). Error is the fai..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExpectedFailureRecord"
  hidden: true
---

<a id="E2E_Reports_ExpectedFailureRecord"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ExpectedFailureRecord : IEquatable<ExpectedFailureRecord>
```


Why an attempt or a test was expected to fail (upstream's inline `expectedFailure` object of `ReportAttempt` and `ReportResult`,
`report/build.ts`). [Error](#E2E_Reports_ExpectedFailureRecord_Error) is the failure that made the attempt pass, kept on the attempt only.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExpectedFailureRecord&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs)

## Properties

<a id="E2E_Reports_ExpectedFailureRecord_Error_"></a>
<a id="E2E_Reports_ExpectedFailureRecord_Error"></a>

### Error


```csharp
public ReportError? Error { get; init; }
```


The failure that made a marked attempt pass. Null when the attempt did not fail with an error the mark covers.

**Value:** [ReportError](/api/E2E.Reports/ReportError/)

<a id="E2E_Reports_ExpectedFailureRecord_Reason_"></a>
<a id="E2E_Reports_ExpectedFailureRecord_Reason"></a>

### Reason


```csharp
public string Reason { get; init; }
```


The reason the test is marked expected to fail.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
