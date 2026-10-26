---
title: "ReportTarget"
description: "One target of the run (upstream's ReportTarget)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportTarget"
  hidden: true
---

<a id="E2E_Reports_ReportTarget"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ReportTarget : IEquatable<ReportTarget>
```


One target of the run (upstream's `ReportTarget`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportTarget&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L149)

## Properties

<a id="E2E_Reports_ReportTarget_ArtifactCapabilities_"></a>
<a id="E2E_Reports_ReportTarget_ArtifactCapabilities"></a>

### ArtifactCapabilities


```csharp
public IReadOnlyList<ArtifactCapability> ArtifactCapabilities { get; init; }
```


**Value:** [IReadOnlyList&lt;ArtifactCapability&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L168)

<a id="E2E_Reports_ReportTarget_BaseOrigin_"></a>
<a id="E2E_Reports_ReportTarget_BaseOrigin"></a>

### BaseOrigin


```csharp
public string? BaseOrigin { get; init; }
```


Origin of the target's declared app URL; null for a surface without one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L159)

<a id="E2E_Reports_ReportTarget_Capabilities_"></a>
<a id="E2E_Reports_ReportTarget_Capabilities"></a>

### Capabilities


```csharp
public IReadOnlyList<string> Capabilities { get; init; }
```


What the engine can do, sorted.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L166)

<a id="E2E_Reports_ReportTarget_Engine_"></a>
<a id="E2E_Reports_ReportTarget_Engine"></a>

### Engine


```csharp
public ReportEngine Engine { get; init; }
```


**Value:** [ReportEngine](/api/E2E.Reports/ReportEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L163)

<a id="E2E_Reports_ReportTarget_Environment_"></a>
<a id="E2E_Reports_ReportTarget_Environment"></a>

### Environment


```csharp
public TargetEnvironment Environment { get; init; }
```


**Value:** [TargetEnvironment](/api/E2E.Reports/TargetEnvironment/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L161)

<a id="E2E_Reports_ReportTarget_Id_"></a>
<a id="E2E_Reports_ReportTarget_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L151)

<a id="E2E_Reports_ReportTarget_Index_"></a>
<a id="E2E_Reports_ReportTarget_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


Its place in the config's targets.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L154)

<a id="E2E_Reports_ReportTarget_Platform_"></a>
<a id="E2E_Reports_ReportTarget_Platform"></a>

### Platform


```csharp
public string Platform { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L156)

<a id="E2E_Reports_ReportTarget_StateCapability_"></a>
<a id="E2E_Reports_ReportTarget_StateCapability"></a>

### StateCapability


```csharp
public bool StateCapability { get; init; }
```


Whether the engine can capture and restore state.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L171)
