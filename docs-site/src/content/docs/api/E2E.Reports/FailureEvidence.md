---
title: "FailureEvidence"
description: "What the runner saw when a failure landed (upstream's FailureEvidence)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FailureEvidence"
  hidden: true
---

<a id="E2E_Reports_FailureEvidence"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record FailureEvidence : IEquatable<FailureEvidence>
```


What the runner saw when a failure landed (upstream's `FailureEvidence`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;FailureEvidence&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L389)

## Properties

<a id="E2E_Reports_FailureEvidence_Candidates_"></a>
<a id="E2E_Reports_FailureEvidence_Candidates"></a>

### Candidates


```csharp
public IReadOnlyList<string>? Candidates { get; init; }
```


The nodes closest to what a failed locator asked for.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L406)

<a id="E2E_Reports_FailureEvidence_Nodes_"></a>
<a id="E2E_Reports_FailureEvidence_Nodes"></a>

### Nodes


```csharp
public int? Nodes { get; init; }
```


Nodes the screen listed.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L397)

<a id="E2E_Reports_FailureEvidence_Screen_"></a>
<a id="E2E_Reports_FailureEvidence_Screen"></a>

### Screen


```csharp
public string? Screen { get; init; }
```


The id of the artifact that holds the screen as the agent reads it, one node per line.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L400)

<a id="E2E_Reports_FailureEvidence_Screenshot_"></a>
<a id="E2E_Reports_FailureEvidence_Screenshot"></a>

### Screenshot


```csharp
public string? Screenshot { get; init; }
```


The id of the artifact that holds a screenshot of the screen.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L403)

<a id="E2E_Reports_FailureEvidence_Url_"></a>
<a id="E2E_Reports_FailureEvidence_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


The page or screen the app was on.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L392)

<a id="E2E_Reports_FailureEvidence_Viewport_"></a>
<a id="E2E_Reports_FailureEvidence_Viewport"></a>

### Viewport


```csharp
public ViewportSize? Viewport { get; init; }
```


**Value:** [ViewportSize](/api/E2E.Reports/ViewportSize/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L394)
