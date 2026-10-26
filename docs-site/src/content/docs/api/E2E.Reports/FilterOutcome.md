---
title: "FilterOutcome"
description: "Why a filter left nothing out without answering (upstream's FilterOutcome)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FilterOutcome"
  hidden: true
---

<a id="E2E_Reports_FilterOutcome"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public enum FilterOutcome
```


Why a filter left nothing out without answering (upstream's `FilterOutcome`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L921)

## Fields

<a id="E2E_Reports_FilterOutcome_Failed"></a>

### Failed


```csharp
Failed = 0
```


It threw or answered with something invalid.

**Value:** [FilterOutcome](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L924)

<a id="E2E_Reports_FilterOutcome_TimedOut"></a>

### TimedOut


```csharp
TimedOut = 1
```


It ran past 5 minutes and the run stopped waiting.

**Value:** [FilterOutcome](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L927)
