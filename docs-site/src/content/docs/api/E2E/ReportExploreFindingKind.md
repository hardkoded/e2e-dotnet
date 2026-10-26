---
title: "ReportExploreFindingKind"
description: "Whether a finding fails the exploration."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExploreFindingKind"
  hidden: true
---

<a id="E2E_ReportExploreFindingKind"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ReportExploreFindingKind
```


Whether a finding fails the exploration.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L8)

## Fields

<a id="E2E_ReportExploreFindingKind_Issue"></a>

### Issue


```csharp
Issue = 0
```


A defect a user would hit. It fails the run.

**Value:** [ReportExploreFindingKind](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L11)

<a id="E2E_ReportExploreFindingKind_Warning"></a>

### Warning


```csharp
Warning = 1
```


Cosmetic or polish. It is recorded and does not fail the run.

**Value:** [ReportExploreFindingKind](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs#L14)
