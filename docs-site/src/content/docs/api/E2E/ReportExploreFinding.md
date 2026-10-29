---
title: "ReportExploreFinding"
description: "One finding Explore recorded through its reportfinding tool."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExploreFinding"
  hidden: true
---

<a id="E2E_ReportExploreFinding"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ReportExploreFinding : IEquatable<ReportExploreFinding>
```


One finding [Explore](/api/E2E/Explore/) recorded through its `report_finding` tool.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportExploreFinding&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L59)

## Constructors

<a id="E2E_ReportExploreFinding__ctor_"></a>
<a id="E2E_ReportExploreFinding__ctor_System_String_System_Int32_System_Nullable_System_Int32__E2E_ReportExploreFindingKind_System_Int32_System_String_System_String_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_DateTimeOffset_System_String_System_String_System_String_"></a>

### `ReportExploreFinding(string, int, int?, ReportExploreFindingKind, int, string, string, string, IReadOnlyList<string>, DateTimeOffset, string?, string?, string?)`


```csharp
public ReportExploreFinding(string Id, int Index, int? Step, ReportExploreFindingKind Kind, int Severity, string Title, string Expected, string Actual, IReadOnlyList<string> Reproduction, DateTimeOffset ReportedAt, string? Path = null, string? ArtifactId = null, string? ObservationRevision = null)
```


One finding [Explore](/api/E2E/Explore/) recorded through its `report_finding` tool.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) | A time-ordered unique id. |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | Zero-based position among the findings. |
| `Step` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) | One-based exploration step the finding was reported in. Null when reported between steps. |
| `Kind` | [ReportExploreFindingKind](/api/E2E/ReportExploreFindingKind/) | An issue fails the run; a warning is recorded and does not. |
| `Severity` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | 5 is a core journey made impossible or data made wrong; 1 is a polish item. |
| `Title` | [string](https://learn.microsoft.com/dotnet/api/system.string) | A specific headline, at most 200 characters. |
| `Expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What a user would expect, at most 2000 characters. |
| `Actual` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the screen shows instead, at most 2000 characters. |
| `Reproduction` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The actions that reach the defect, at most 20, each at most 500 characters. |
| `ReportedAt` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) | When the finding was recorded. |
| `Path` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The redacted location of the screen, when the engine reports one. |
| `ArtifactId` | [string](https://learn.microsoft.com/dotnet/api/system.string) | Where the evidence screenshot was kept, as a path relative to the project root, when pixels were granted. Upstream names an artifact of the report; the port has no report, so it names the file. |
| `ObservationRevision` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The revision of the screen the finding was reported on ([Revision](/api/E2E/ExecutorObservation/#E2E_ExecutorObservation_Revision)), when the finding tool observed one. The tool takes its own capture, as upstream does, so the revision is that capture's and is not one an agent step recorded. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L59)

## Properties

<a id="E2E_ReportExploreFinding_Actual_"></a>
<a id="E2E_ReportExploreFinding_Actual"></a>

### Actual


```csharp
public string Actual { get; init; }
```


What the screen shows instead, at most 2000 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L67)

<a id="E2E_ReportExploreFinding_ArtifactId_"></a>
<a id="E2E_ReportExploreFinding_ArtifactId"></a>

### ArtifactId


```csharp
public string? ArtifactId { get; init; }
```


Where the evidence screenshot was kept, as a path relative to the project root, when pixels were granted. Upstream names an artifact of the report; the port has no report, so it names the file.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L71)

<a id="E2E_ReportExploreFinding_Expected_"></a>
<a id="E2E_ReportExploreFinding_Expected"></a>

### Expected


```csharp
public string Expected { get; init; }
```


What a user would expect, at most 2000 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L66)

<a id="E2E_ReportExploreFinding_Id_"></a>
<a id="E2E_ReportExploreFinding_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


A time-ordered unique id.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L60)

<a id="E2E_ReportExploreFinding_Index_"></a>
<a id="E2E_ReportExploreFinding_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


Zero-based position among the findings.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L61)

<a id="E2E_ReportExploreFinding_Kind_"></a>
<a id="E2E_ReportExploreFinding_Kind"></a>

### Kind


```csharp
public ReportExploreFindingKind Kind { get; init; }
```


An issue fails the run; a warning is recorded and does not.

**Value:** [ReportExploreFindingKind](/api/E2E/ReportExploreFindingKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L63)

<a id="E2E_ReportExploreFinding_ObservationRevision_"></a>
<a id="E2E_ReportExploreFinding_ObservationRevision"></a>

### ObservationRevision


```csharp
public string? ObservationRevision { get; init; }
```


The revision of the screen the finding was reported on ([Revision](/api/E2E/ExecutorObservation/#E2E_ExecutorObservation_Revision)), when the finding tool observed one. The tool takes its own capture, as upstream does, so the revision is that capture's and is not one an agent step recorded.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L72)

<a id="E2E_ReportExploreFinding_Path_"></a>
<a id="E2E_ReportExploreFinding_Path"></a>

### Path


```csharp
public string? Path { get; init; }
```


The redacted location of the screen, when the engine reports one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L70)

<a id="E2E_ReportExploreFinding_ReportedAt_"></a>
<a id="E2E_ReportExploreFinding_ReportedAt"></a>

### ReportedAt


```csharp
public DateTimeOffset ReportedAt { get; init; }
```


When the finding was recorded.

**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L69)

<a id="E2E_ReportExploreFinding_Reproduction_"></a>
<a id="E2E_ReportExploreFinding_Reproduction"></a>

### Reproduction


```csharp
public IReadOnlyList<string> Reproduction { get; init; }
```


The actions that reach the defect, at most 20, each at most 500 characters.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L68)

<a id="E2E_ReportExploreFinding_Severity_"></a>
<a id="E2E_ReportExploreFinding_Severity"></a>

### Severity


```csharp
public int Severity { get; init; }
```


5 is a core journey made impossible or data made wrong; 1 is a polish item.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L64)

<a id="E2E_ReportExploreFinding_Step_"></a>
<a id="E2E_ReportExploreFinding_Step"></a>

### Step


```csharp
public int? Step { get; init; }
```


One-based exploration step the finding was reported in. Null when reported between steps.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L62)

<a id="E2E_ReportExploreFinding_Title_"></a>
<a id="E2E_ReportExploreFinding_Title"></a>

### Title


```csharp
public string Title { get; init; }
```


A specific headline, at most 200 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L65)
