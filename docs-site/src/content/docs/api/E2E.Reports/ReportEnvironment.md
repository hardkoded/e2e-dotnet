---
title: "ReportEnvironment"
description: "What a run ran on (upstream's run.environment)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportEnvironment"
  hidden: true
---

<a id="E2E_Reports_ReportEnvironment"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportEnvironment : IEquatable<ReportEnvironment>
```


What a run ran on (upstream's `run.environment`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportEnvironment&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L88)

## Properties

<a id="E2E_Reports_ReportEnvironment_Arch_"></a>
<a id="E2E_Reports_ReportEnvironment_Arch"></a>

### Arch


```csharp
public string Arch { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L97)

<a id="E2E_Reports_ReportEnvironment_Ci_"></a>
<a id="E2E_Reports_ReportEnvironment_Ci"></a>

### Ci


```csharp
public bool Ci { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L90)

<a id="E2E_Reports_ReportEnvironment_Os_"></a>
<a id="E2E_Reports_ReportEnvironment_Os"></a>

### Os


```csharp
public string Os { get; init; }
```


The platform and its release, such as `darwin 24.1.0`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L95)

<a id="E2E_Reports_ReportEnvironment_Runtime_"></a>
<a id="E2E_Reports_ReportEnvironment_Runtime"></a>

### Runtime


```csharp
public string Runtime { get; init; }
```


The runtime, such as `.NET 10.0.0`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L100)

<a id="E2E_Reports_ReportEnvironment_TrustNoticeShown_"></a>
<a id="E2E_Reports_ReportEnvironment_TrustNoticeShown"></a>

### TrustNoticeShown


```csharp
public bool TrustNoticeShown { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L92)
