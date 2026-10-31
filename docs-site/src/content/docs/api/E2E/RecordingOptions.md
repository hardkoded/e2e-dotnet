---
title: "RecordingOptions"
description: "How CancellationToken) names its file (upstream's RecordingOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordingOptions"
  hidden: true
---

<a id="E2E_RecordingOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record RecordingOptions : IEquatable<RecordingOptions>
```


How `CancellationToken)` names its file (upstream's `RecordingOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RecordingOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L15)

## Properties

<a id="E2E_RecordingOptions_Name_"></a>
<a id="E2E_RecordingOptions_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


File name stem, `recordings/001-<name>.<ext>`; defaults to `recording`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Recordings.cs#L18)
