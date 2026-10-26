---
title: "AppLogRecord"
description: "One line the app logged (upstream's AppLogRecord)."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppLogRecord"
  hidden: true
---

<a id="E2E_Reports_AppLogRecord"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record AppLogRecord : IEquatable<AppLogRecord>
```


One line the app logged (upstream's `AppLogRecord`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;AppLogRecord&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L482)

## Properties

<a id="E2E_Reports_AppLogRecord_At_"></a>
<a id="E2E_Reports_AppLogRecord_At"></a>

### At


```csharp
public DateTimeOffset At { get; init; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L490)

<a id="E2E_Reports_AppLogRecord_Level_"></a>
<a id="E2E_Reports_AppLogRecord_Level"></a>

### Level


```csharp
public AppLogLevel Level { get; init; }
```


**Value:** [AppLogLevel](/api/E2E.Reports/AppLogLevel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L486)

<a id="E2E_Reports_AppLogRecord_Source_"></a>
<a id="E2E_Reports_AppLogRecord_Source"></a>

### Source


```csharp
public AppLogSource Source { get; init; }
```


**Value:** [AppLogSource](/api/E2E.Reports/AppLogSource/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L484)

<a id="E2E_Reports_AppLogRecord_Step_"></a>
<a id="E2E_Reports_AppLogRecord_Step"></a>

### Step


```csharp
public int? Step { get; init; }
```


The index of the step the line belongs to; null before the first step.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L493)

<a id="E2E_Reports_AppLogRecord_Text_"></a>
<a id="E2E_Reports_AppLogRecord_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L488)
