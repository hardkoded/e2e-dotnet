---
title: "CacheInfo"
description: "CacheInfo in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "CacheInfo"
  hidden: true
---

<a id="E2E_CacheInfo"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CacheInfo
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1016)

## Properties

<a id="E2E_CacheInfo_Detail_"></a>
<a id="E2E_CacheInfo_Detail"></a>

### Detail


```csharp
public string? Detail { get; init; }
```


Where the replay stopped and why, after a hand-off. After an `end-mismatch` it names the recorded nodes that did not show, the ones that did not go away, and any alert the recording never saw, or says the outcome already showed before the replay acted. Null when the replay gave no detail.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1029)

<a id="E2E_CacheInfo_Entry_"></a>
<a id="E2E_CacheInfo_Entry"></a>

### Entry


```csharp
public string? Entry { get; init; }
```


The step's cache key, which names its entry file (`<directory>/<entry>.json` in a [FileStepCache](/api/E2E/FileStepCache/)).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1035)

<a id="E2E_CacheInfo_Mode_"></a>
<a id="E2E_CacheInfo_Mode"></a>

### Mode


```csharp
public required string Mode { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1018)

<a id="E2E_CacheInfo_NotRecorded_"></a>
<a id="E2E_CacheInfo_NotRecorded"></a>

### NotRecorded


```csharp
public string? NotRecorded { get; init; }
```


`param-collision` when the step passed but its recording was not written, because a `unique()` value is spelled by another param.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1032)

<a id="E2E_CacheInfo_Reason_"></a>
<a id="E2E_CacheInfo_Reason"></a>

### Reason


```csharp
public string? Reason { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1020)

<a id="E2E_CacheInfo_RecordedBy_"></a>
<a id="E2E_CacheInfo_RecordedBy"></a>

### RecordedBy


```csharp
public string? RecordedBy { get; init; }
```


The configured model that recorded the entry a step replayed, as [Name](/api/E2E/IAgentModel/#E2E_IAgentModel_Name) spells it. Null for a step that read no entry, and for an entry with no attribution.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1038)

<a id="E2E_CacheInfo_ReplayedActions_"></a>
<a id="E2E_CacheInfo_ReplayedActions"></a>

### ReplayedActions


```csharp
public int ReplayedActions { get; init; }
```


The recorded actions the replay ran. 0 for a miss.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1023)

<a id="E2E_CacheInfo_TotalActions_"></a>
<a id="E2E_CacheInfo_TotalActions"></a>

### TotalActions


```csharp
public int TotalActions { get; init; }
```


The actions the recording holds. 0 when there was no recording to read.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L1026)
