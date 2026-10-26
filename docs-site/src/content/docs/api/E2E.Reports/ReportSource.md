---
title: "ReportSource"
description: "A line in a file of the project (upstream's ReportSource)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportSource"
  hidden: true
---

<a id="E2E_Reports_ReportSource"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportSource : IEquatable<ReportSource>
```


A line in a file of the project (upstream's `ReportSource`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportSource&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L110)

## Properties

<a id="E2E_Reports_ReportSource_Column_"></a>
<a id="E2E_Reports_ReportSource_Column"></a>

### Column


```csharp
public int Column { get; init; }
```


One-based.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L119)

<a id="E2E_Reports_ReportSource_File_"></a>
<a id="E2E_Reports_ReportSource_File"></a>

### File


```csharp
public string File { get; init; }
```


Relative to the project root, with `/` separators.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L113)

<a id="E2E_Reports_ReportSource_Line_"></a>
<a id="E2E_Reports_ReportSource_Line"></a>

### Line


```csharp
public int Line { get; init; }
```


One-based.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L116)
