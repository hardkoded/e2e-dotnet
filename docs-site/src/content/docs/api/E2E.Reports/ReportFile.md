---
title: "ReportFile"
description: "One file a reporter's ReportFilesAsync asks the runner to write (upstream's ReportFile, run-types.ts). The runner writes it atomically, creating its director..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportFile"
  hidden: true
---

<a id="E2E_Reports_ReportFile"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportFile : IEquatable<ReportFile>
```


One file a reporter's `ReportFilesAsync` asks the runner to write (upstream's `ReportFile`, `run-types.ts`). The runner writes it
atomically, creating its directory, and its row prints under the summary when it has a [Label](#E2E_Reports_ReportFile_Label). Inside `namespace E2E` the internal
`E2E.ReportFile` (the .NET-only `report.json` store) wins the name, so code there writes `E2E.Reports.ReportFile`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportFile&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Constructors

<a id="E2E_Reports_ReportFile__ctor_"></a>
<a id="E2E_Reports_ReportFile__ctor_System_String_System_String_"></a>

### ReportFile(string, string)


```csharp
public ReportFile(string Path, string Content)
```


One file a reporter's `ReportFilesAsync` asks the runner to write (upstream's `ReportFile`, `run-types.ts`). The runner writes it
atomically, creating its directory, and its row prints under the summary when it has a [Label](#E2E_Reports_ReportFile_Label). Inside `namespace E2E` the internal
`E2E.ReportFile` (the .NET-only `report.json` store) wins the name, so code there writes `E2E.Reports.ReportFile`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Path` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Where it goes: absolute, or relative to the project root. Never `report.json` itself. |
| `Content` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The text to write. |

## Properties

<a id="E2E_Reports_ReportFile_Content_"></a>
<a id="E2E_Reports_ReportFile_Content"></a>

### Content


```csharp
public string Content { get; init; }
```


The text to write.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReportFile_Label_"></a>
<a id="E2E_Reports_ReportFile_Label"></a>

### Label


```csharp
public string? Label { get; init; }
```


The summary row's label; its text is the path from the project root. No row without one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReportFile_Note_"></a>
<a id="E2E_Reports_ReportFile_Note"></a>

### Note


```csharp
public string? Note { get; init; }
```


Printed after the path in the summary row, such as `(one file, 2.1 MB)`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_ReportFile_Path_"></a>
<a id="E2E_Reports_ReportFile_Path"></a>

### Path


```csharp
public string Path { get; init; }
```


Where it goes: absolute, or relative to the project root. Never `report.json` itself.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
