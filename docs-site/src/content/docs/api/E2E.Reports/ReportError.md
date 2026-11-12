---
title: "ReportError"
description: "An error as a report tells it: the serialized error without its stack (upstream's ReportError)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportError"
  hidden: true
---

<a id="E2E_Reports_ReportError"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportError : IEquatable<ReportError>
```


An error as a report tells it: the serialized error without its stack (upstream's `ReportError`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportError&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs)

## Properties

<a id="E2E_Reports_ReportError_Category_"></a>
<a id="E2E_Reports_ReportError_Category"></a>

### Category


```csharp
public ErrorCategory Category { get; init; }
```


**Value:** [ErrorCategory](/api/E2E.Reports/ErrorCategory/)

<a id="E2E_Reports_ReportError_Code_"></a>
<a id="E2E_Reports_ReportError_Code"></a>

### Code


```csharp
public string Code { get; init; }
```


The error code, such as `ASSERTION_FAILED`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReportError_Details_"></a>
<a id="E2E_Reports_ReportError_Details"></a>

### Details


```csharp
public ErrorDetails? Details { get; init; }
```


The structured facts beside the message: an assertion's expected and observed, a locator's role and name.

**Value:** [ErrorDetails](/api/E2E/ErrorDetails/)

<a id="E2E_Reports_ReportError_Message_"></a>
<a id="E2E_Reports_ReportError_Message"></a>

### Message


```csharp
public string Message { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReportError_Phase_"></a>
<a id="E2E_Reports_ReportError_Phase"></a>

### Phase


```csharp
public ErrorPhase? Phase { get; init; }
```


**Value:** `ErrorPhase?`

<a id="E2E_Reports_ReportError_Retryable_"></a>
<a id="E2E_Reports_ReportError_Retryable"></a>

### Retryable


```csharp
public bool Retryable { get; init; }
```


Whether the caller may observe again and repeat the operation.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Reports_ReportError_Scope_"></a>
<a id="E2E_Reports_ReportError_Scope"></a>

### Scope


```csharp
public HookScope? Scope { get; init; }
```


**Value:** [HookScope](/api/E2E.Reports/HookScope/)

<a id="E2E_Reports_ReportError_ScopeId_"></a>
<a id="E2E_Reports_ReportError_ScopeId"></a>

### ScopeId


```csharp
public string? ScopeId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReportError_Source_"></a>
<a id="E2E_Reports_ReportError_Source"></a>

### Source


```csharp
public ReportSource? Source { get; init; }
```


The line in the test file the failure unwound through.

**Value:** [ReportSource](/api/E2E.Reports/ReportSource/)
